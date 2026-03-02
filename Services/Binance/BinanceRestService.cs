using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.Services.Binance
{
    public class BinanceRestService : IRestService
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public async Task<List<CommonCandle>> GetCandlesAsync(string market, int count)
        {
            try
            {
                string symbol = market.Replace("KRW-", "").ToUpper() + "USDT";
                string url = $"https://api.binance.com/api/v3/klines?symbol={symbol}&interval=1m&limit={count}";
                string json = await _httpClient.GetStringAsync(url);

                // ⭐ JsonSerializer 대신 JsonDocument로 배열 순회
                using var doc = JsonDocument.Parse(json);
                var candles = new List<CommonCandle>();

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    candles.Add(new CommonCandle
                    {
                        Time = DateTimeOffset.FromUnixTimeMilliseconds(item[0].GetInt64()).LocalDateTime,
                        Open = double.Parse(item[1].GetString() ?? "0"),
                        High = double.Parse(item[2].GetString() ?? "0"),
                        Low = double.Parse(item[3].GetString() ?? "0"),
                        Close = double.Parse(item[4].GetString() ?? "0"),
                        Volume = double.Parse(item[5].GetString() ?? "0")
                    });
                }
                return candles;
            }
            catch { return new List<CommonCandle>(); }
        }

        public Task<List<AssetItem>> GetAccountsAsync() => Task.FromResult(new List<AssetItem>());
        public Task<string> GetAccountsJsonAsync() => Task.FromResult("[]");

        public Task<string> GetOpenOrdersJsonAsync()
        {
            throw new NotImplementedException();
        }
    }
}