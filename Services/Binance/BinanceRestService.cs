using System.Text.Json;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Binance; // BinanceCandleDto가 위치한 곳
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.Services.Binance
{
    public class BinanceRestService : IRestService
    {
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public async Task<List<CommonCandle>> GetCandlesAsync(string market, int count)
        {
            try
            {
                // 1. 바이낸스 API 호출 (예: symbol=BTCUSDT, interval=1m)
                string binanceSymbol = market.Replace("KRW-", "").Replace("-", "") + "USDT";
                string url = $"https://api.binance.com/api/v3/klines?symbol={binanceSymbol}&interval=1m&limit={count}";

                string json = await _httpClient.GetStringAsync(url);

                // 2. 바이낸스 특유의 이중 배열 JSON 파싱
                using var doc = JsonDocument.Parse(json);
                var candles = new List<CommonCandle>();

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    // 바이낸스 kline 배열 인덱스: 0:OpenTime, 1:Open, 2:High, 3:Low, 4:Close, 5:Volume
                    candles.Add(new CommonCandle
                    {
                        Time = DateTimeOffset.FromUnixTimeMilliseconds(item[0].GetInt64()).DateTime.ToLocalTime(),
                        Open = double.Parse(item[1].GetString() ?? "0"),
                        High = double.Parse(item[2].GetString() ?? "0"),
                        Low = double.Parse(item[3].GetString() ?? "0"),
                        Close = double.Parse(item[4].GetString() ?? "0"),
                        Volume = double.Parse(item[5].GetString() ?? "0")
                    });
                }

                return candles;
            }
            catch (Exception ex)
            {
                // Logger.Error(ex, "Binance 캔들 조회 실패");
                return new List<CommonCandle>();
            }
        }

        // 자산 조회는 바이낸스 API Key와 서명(Signature) 로직이 복잡하므로 
        // 우선 빈 리스트를 반환하거나 나중에 구현합니다.
        public Task<List<AssetItem>> GetAccountsAsync()
        {
            return Task.FromResult(new List<AssetItem>());
        }

   
    }
}