using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;
using Upbit_Manager.Core;

namespace Upbit_Manager.Services.Upbit
{

    public class UpbitRestService : IRestService
    {
        private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.upbit.com") };
        private readonly UpbitAuthenticator _authenticator;

        public UpbitRestService(string accessKey, string secretKey)
        {
            _authenticator = new UpbitAuthenticator(accessKey, secretKey);
        }

        public async Task<List<CommonCandle>> GetCandlesAsync(string market, int count)
        {
            try
            {
                var path = $"/v1/candles/minutes/1?market={market}&count={count}";
                var res = await _http.GetAsync(path);
                res.EnsureSuccessStatusCode();

                var json = await res.Content.ReadAsStringAsync();
                var dtos = JsonSerializer.Deserialize<List<UpbitCandleDto>>(json);

                if (dtos == null) return new List<CommonCandle>();

                return dtos.Select(d => new CommonCandle
                {
                    Time = d.CandleDateTimeKst,
                    Open = d.OpeningPrice,
                    High = d.HighPrice,
                    Low = d.LowPrice,
                    Close = d.TradePrice,
                    Volume = d.CandleAccTradeVolume
                }).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"캔들 조회 실패: {ex.Message}", "ERROR");
                return new List<CommonCandle>();
            }
        }

        public async Task<List<AssetItem>> GetAccountsAsync()
        {
            try
            {
                const string path = "/v1/accounts";
                string jwtToken = _authenticator.CreateJwtToken();

                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

                var res = await _http.SendAsync(request);
                res.EnsureSuccessStatusCode();

                var json = await res.Content.ReadAsStringAsync();
                var dtos = JsonSerializer.Deserialize<List<UpbitAccountDto>>(json);

                if (dtos == null) return new List<AssetItem>();

                // [수정 포인트] ?? 연산자 에러 해결 및 안전한 형변환
                return dtos.Select(d => new AssetItem
                {
                    Exchange = "Upbit",
                    Symbol = d.Currency ?? "Unknown",
                    // d.Balance가 string이면 double.Parse를, 숫자형이면 직접 대입하세요.
                    // 여기서는 d.Balance가 숫자형(double/decimal)이라고 가정하여 수정했습니다.
                    TotalInventory = Convert.ToDouble(d.Balance),
                    AvgBuyPrice = Convert.ToDouble(d.AvgBuyPrice),
                    CurrentPrice = 0
                }).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"계좌 조회 실패: {ex.Message}", "ERROR");
                return new List<AssetItem>();
            }
        }



        // UpbitRestService.cs 내부

        /// <summary>
        /// 업비트 API 공통 호출 메서드 (인증 포함)
        /// </summary>
        // UpbitRestService.cs 내부
        private async Task<string> CallApiAsync(string url)
        {
            try
            {
                var uri = new Uri(url);
                // "state=wait" 같은 부분을 추출합니다.
                string queryString = uri.Query.TrimStart('?');

                // [중요] 쿼리 스트링을 CreateJwtToken에 전달합니다!
                string jwtToken = _authenticator.CreateJwtToken(queryString);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

                var res = await _http.SendAsync(request);

                if (!res.IsSuccessStatusCode)
                {
                    // 401 에러가 나면 여기서 왜 죽었는지 본문을 읽어볼 수 있습니다.
                    string errorContent = await res.Content.ReadAsStringAsync();
                    Logger.Log($"API 에러: {res.StatusCode} - {errorContent}", "ERROR");
                    return string.Empty;
                }

                return await res.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Logger.Log($"API 호출 실패 ({url}): {ex.Message}", "ERROR");
                return string.Empty;
            }
        }

        /// <summary>
        /// 계좌 정보를 JSON 문자열로 반환
        /// </summary>
        public async Task<string> GetAccountsJsonAsync()
        {
            return await CallApiAsync("https://api.upbit.com/v1/accounts");
        }

        /// <summary>
        /// 미체결 주문 목록을 JSON 문자열로 반환
        /// </summary>
        public async Task<string> GetOpenOrdersJsonAsync()
        {
            // state=wait 파라미터가 포함되어야 미체결 주문만 가져옵니다.
            return await CallApiAsync("https://api.upbit.com/v1/orders?state=wait");
        }
    }
}