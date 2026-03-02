using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;
using Upbit_Manager.UI;

namespace Upbit_Manager.Core
{
    /// <summary>
    /// API Key 인증(JWT) 및 사용자의 자산(Asset), 미체결 주문(Order) 데이터를 총괄 관리하는 클래스입니다.
    /// </summary>
    public class AccountManager
    {

        // ⭐ [활용방안]: 자산/평단가 변경 시 UI(그리드, 레이블)를 즉시 갱신하거나 수익률 알람을 트리거하는 데 사용
        public event Action<string, AssetItem>? OnAssetUpdated;

        // ⭐ [활용방안]: 특정 종목을 전량 매도하여 리스트에서 사라질 때 UI 항목을 제거하거나 리스트를 새로고침하기 위해 사용
        public event Action<string>? OnAssetRemoved;

        // ⭐ [활용방안]: 전체 자산 동기화가 완료되었을 때 총 평가금액 등을 한꺼번에 업데이트하기 위해 사용
        public event Action? OnAllAssetsSynced;

        #region [ 보안 및 인증 관련 필드 ]

        private readonly string _accessKey;
        private readonly string _secretKey;
        private static readonly string KeyFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Upbit_Manager", "ApiKey.cfg");

        #endregion

        #region [ 데이터 관리 필드 ]

        // 보유 자산 리스트 (키: "KRW", "BTC" 등의 심볼)
        public Dictionary<string, AssetItem> MyAssets { get; private set; } = new();

        // 미체결 주문 리스트 (스레드 안전을 위해 lock 사용 권장)
        public List<UpbitOpenOrder> CurrentOpenOrders { get; private set; } = new();

        #endregion

        public AccountManager()
        {
            // 1. 키 파일 초기화 및 로드
            InitializeKeyFile();
            var lines = File.ReadAllLines(KeyFilePath);
            _accessKey = lines.Length > 0 ? lines[0].Trim() : string.Empty;
            _secretKey = lines.Length > 1 ? lines[1].Trim() : string.Empty;
        }

        private void InitializeKeyFile()
        {
            if (!File.Exists(KeyFilePath))
            {
                var dir = Path.GetDirectoryName(KeyFilePath);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(KeyFilePath, "YOUR_ACCESS_KEY\nYOUR_SECRET_KEY");
            }
        }

        #region [ 인증 토큰 생성 ]

        /// <summary>
        /// 업비트 API 호출을 위한 JWT 토큰을 생성합니다.
        /// </summary>
        public string GetJwtToken(string queryString = null)
        {
            var payload = new JwtPayload
            {
                { "access_key", _accessKey },
                { "nonce", Guid.NewGuid().ToString() }
            };

            if (!string.IsNullOrEmpty(queryString))
            {
                using var sha = SHA512.Create();
                var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(queryString))).Replace("-", "").ToLower();
                payload.Add("query_hash", hash);
                payload.Add("query_hash_alg", "SHA512");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);
            var header = new JwtHeader(credentials);

            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
        }

        #endregion

        #region [ 실시간 데이터 업데이트 ]

        /// <summary>
        /// 웹소켓에서 수신한 실시간 가격을 자산 모델에 반영합니다.
        /// </summary>
        public void UpdateCurrentPrice(string market, double price)
        {
            string symbol = market.Replace("KRW-", "").ToUpper();

            if (MyAssets.TryGetValue(symbol, out var asset))
            {
                asset.CurrentPrice = price;
            }
        }

        /// <summary>
        /// 구독이 필요한 보유 코인들의 마켓 리스트를 반환합니다.
        /// </summary>
        public string[] GetSubscribingMarkets()
        {
            return MyAssets.Keys
                .Where(symbol => symbol != "KRW")
                .Select(symbol => $"KRW-{symbol}")
                .ToArray();
        }

        #endregion

        #region [ 자산 및 주문 데이터 동기화 ]

        /// <summary>
        /// 업비트 계좌 API(JSON) 결과를 받아 자산 정보를 갱신합니다.
        /// </summary>
        public void UpdateAssetsFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "AUTH_FAILED" || json == "ERROR") return;
            if (!json.Trim().StartsWith("[")) return;

            try
            {
                var dtos = JsonSerializer.Deserialize<List<UpbitAccountDto>>(json);
                if (dtos != null) UpdateAssetsFromDto(dtos);
            }
            catch (Exception ex)
            {
                Logger.Log($"UpdateAssetsFromJson 역직렬화 오류: {ex.Message}", "ERROR");
            }
        }

        public void UpdateAssetsFromDto(List<UpbitAccountDto> dtos)
        {
            if (dtos == null) return;

            foreach (var dto in dtos)
            {
                string currency = dto.Currency.ToUpper();

                if (!MyAssets.ContainsKey(currency))
                {
                    MyAssets[currency] = new AssetItem { Symbol = currency, Exchange = "Upbit" };
                }

                var asset = MyAssets[currency];
                asset.TotalInventory = dto.Balance + dto.Locked;
                asset.AvgBuyPrice = dto.AvgBuyPrice;

                if (currency == "KRW") asset.CurrentPrice = 1.0;
            }
        }

        public void UpdateAssets(List<AssetItem> assets)
        {
            if (assets == null) return;
            foreach (var asset in assets) MyAssets[asset.Symbol] = asset;
        }

        /// <summary>
        /// 미체결 주문 데이터를 갱신합니다.
        /// </summary>
        public void UpdateOpenOrders(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "AUTH_FAILED" || !json.Trim().StartsWith("[")) return;

            try
            {
                var newList = JsonSerializer.Deserialize<List<UpbitOpenOrder>>(json);
                if (newList != null)
                {
                    lock (CurrentOpenOrders)
                    {
                        CurrentOpenOrders = newList;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"UpdateOpenOrders 역직렬화 오류: {ex.Message}", "ERROR");
            }
        }

        #endregion

        #region [ 조회 및 통계 메서드 ]

        public double GetTotalBuyAmount() => MyAssets.Values
            .Where(a => a.Symbol != "KRW")
            .Sum(a => a.TotalBuyAmount);

        public double GetTotalEvaluationAmount() => MyAssets.Values
            .Where(a => a.Symbol != "KRW")
            .Sum(a => a.EvaluationAmount);

        public double GetTotalAssetValue()
        {
            double cash = MyAssets.TryGetValue("KRW", out var krwAsset) ? krwAsset.TotalInventory : 0;
            return cash + GetTotalEvaluationAmount();
        }

        public List<UpbitOpenOrder> GetOpenOrdersByMarket(string market)
        {
            lock (CurrentOpenOrders)
            {
                return CurrentOpenOrders.Where(o => o.Market == market).ToList();
            }
        }

        public AssetItem? GetAssetByMarket(string market)
        {
            string symbol = market.Replace("KRW-", "").Trim().ToUpper();
            return MyAssets.TryGetValue(symbol, out var asset) ? asset : null;
        }

        public double GetAvgBuyPrice(string market) => GetAssetByMarket(market)?.AvgBuyPrice ?? 0;
        public double GetProfitRate(string market) => GetAssetByMarket(market)?.ProfitRate ?? 0;

        #endregion
    }
}