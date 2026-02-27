using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Upbit_Manager.Services.Common
{
    public class ExchangeRateService
    {
        //private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.exchangerate.host") };

        //public async Task<double> GetUsdToKrwAsync()
        //{
        //    var res = await _http.GetAsync("/latest?base=USD&symbols=KRW");
        //    res.EnsureSuccessStatusCode();
        //    var json = await res.Content.ReadAsStringAsync();
        //    using var doc = System.Text.Json.JsonDocument.Parse(json);
        //    if (doc.RootElement.TryGetProperty("rates", out var rates) && rates.TryGetProperty("KRW", out var krw))
        //        return krw.GetDouble();
        //    return 1.0;
        //}

        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        public async Task<double> GetUsdToKrwAsync1()
        {
            try
            {
                // 업비트 USDT 가격을 '환율' 대용으로 사용 (김프 포함된 실질 환율)
                string url = "https://api.upbit.com/v1/ticker?markets=KRW-USDT";
                string json = await _httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                double tradePrice = doc.RootElement[0].GetProperty("trade_price").GetDouble();

                return tradePrice;
            }
            catch
            {
                // 실패 시 기본값
                return 1450.0;
            }
        }

        // ExchangeRateService.cs 수정 버전
        public async Task<double> GetUsdToKrwAsync()
        {
            try
            {
                // 네이버 환율과 거의 일치하는 공개 API 주소
                string url = "https://open.er-api.com/v6/latest/USD";
                string json = await _httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("rates", out var rates) &&
                    rates.TryGetProperty("KRW", out var krw))
                {
                    return krw.GetDouble(); // 예: 1345.50
                }
            }
            catch
            {
                return 1350.0; // 실패 시 기본값
            }
            return 1350.0;
        }
    }
}
