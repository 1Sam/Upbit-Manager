using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Upbit_Manager.Models.Common;  // AssetItem 위치
using Upbit_Manager.Models.Upbit;
using UpbitManager.Models.Upbit;   // UpbitAccountDto, UpbitOpenOrder 위치

namespace Upbit_Manager.Core
{
    public class AccountManager
    {
        // 이제 Asset 대신 공통 모델인 AssetItem을 사용합니다.
        // 키: "BTC", "ETH" 등 통화명
        public Dictionary<string, AssetItem> MyAssets { get; private set; } = new();

        // 미체결 주문 리스트
        public List<UpbitOpenOrder> CurrentOpenOrders { get; private set; } = new();

        /// <summary>
        /// 웹소켓 구독을 위해 보유 중인 코인들의 마켓 코드 리스트를 반환 (KRW 제외)
        /// </summary>
        public string[] GetSubscribingMarkets()
        {
            return MyAssets.Keys
                .Where(symbol => symbol != "KRW")
                .Select(symbol => $"KRW-{symbol}")
                .ToArray();
        }

        /// <summary>
        /// 웹소켓에서 수신한 실시간 가격을 자산 모델에 주입
        /// </summary>
        public void UpdateCurrentPrice(string market, double price)
        {
            string symbol = market.Replace("KRW-", "");
            if (MyAssets.TryGetValue(symbol, out var asset))
            {
                asset.CurrentPrice = price;
            }
        }

        /// <summary>
        /// 업비트 계좌 API 데이터를 받아 공통 자산 리스트(AssetItem)를 갱신
        /// </summary>


        #region [합계 계산 메서드]

        // KRW를 제외한 코인들의 총 매수 금액 합계
        public double GetTotalBuyAmount() => MyAssets.Values
            .Where(a => a.Symbol != "KRW")
            .Sum(a => a.TotalBuyAmount);

        // KRW를 제외한 코인들의 총 평가 금액 합계
        public double GetTotalEvaluationAmount() => MyAssets.Values
            .Where(a => a.Symbol != "KRW")
            .Sum(a => a.EvaluationAmount);

        // (보유 현금 + 코인 평가 금액) = 내 총 자산
        public double GetTotalAssetValue()
        {
            double cash = MyAssets.TryGetValue("KRW", out var krwAsset) ? krwAsset.TotalInventory : 0;
            return cash + GetTotalEvaluationAmount();
        }

        #endregion

        /// <summary>
        /// 미체결 주문 데이터를 갱신
        /// </summary>
        public void UpdateOpenOrders(string json)
        { 
            if(json == "") return;
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
                Logger.Error(ex, "UpdateOpenOrders 역직렬화 오류");
            }
        }


        /// <summary>
        /// [추가] 서비스로부터 받은 DTO 리스트를 사용하여 자산 정보를 갱신합니다.
        /// </summary>
        public void UpdateAssetsFromDto(List<UpbitAccountDto> dtos)
        {
            if (dtos == null) return;

            try
            {
                foreach (var dto in dtos)
                {
                    string currency = dto.Currency;

                    // 1. 공통 모델인 AssetItem 생성 또는 가져오기
                    if (!MyAssets.ContainsKey(currency))
                    {
                        MyAssets[currency] = new AssetItem { Symbol = currency, Exchange = "Upbit" };
                    }

                    var asset = MyAssets[currency];

                    // 2. DTO의 값을 공통 모델(AssetItem) 속성에 매핑
                    asset.TotalInventory = dto.Balance + dto.Locked;
                    asset.AvgBuyPrice = dto.AvgBuyPrice;

                    // 현금(KRW)의 현재가는 계산 편의를 위해 1로 고정
                    if (currency == "KRW") asset.CurrentPrice = 1.0;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "UpdateAssetsFromDto 오류");
            }
        }

        /// <summary>
        /// [수정] 기존 JSON 방식이 필요하다면 내부에서 위 메서드를 호출하도록 단순화합니다.
        /// </summary>
        public void UpdateAssetsFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                var dtos = JsonSerializer.Deserialize<List<UpbitAccountDto>>(json);
                UpdateAssetsFromDto(dtos); // 위에서 만든 메서드 재사용
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "UpdateAssetsFromJson 오류");
            }
        }

        // AccountManager 내부
        public void UpdateAssets(List<AssetItem> assets)
        {
            if (assets == null) return;

            foreach (var asset in assets)
            {
                // 이미 서비스에서 AssetItem으로 만들어 왔으므로 그대로 딕셔너리에 넣거나 갱신합니다.
                MyAssets[asset.Symbol] = asset;
            }
        }


        /// <summary>
        /// 특정 마켓(예: KRW-BTC)에 해당하는 미체결 주문만 필터링하여 반환
        /// </summary>
        public List<UpbitOpenOrder> GetOpenOrdersByMarket(string market)
        {
            lock (CurrentOpenOrders)
            {
                // UpbitOpenOrder의 Market 속성과 일치하는 것만 추출
                return CurrentOpenOrders
                    .Where(o => o.Market == market)
                    .ToList();
            }
        }
    }
}