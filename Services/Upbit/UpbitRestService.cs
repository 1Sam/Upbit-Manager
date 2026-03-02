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
        private static readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("https://api.upbit.com"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly UpbitAuthenticator _authenticator;
        private string _lastApiError = ""; // 중복 로그 방지

        public UpbitRestService(string accessKey, string secretKey)
        {
            _authenticator = new UpbitAuthenticator(accessKey, secretKey);
        }

        public async Task<List<CommonCandle>> GetCandlesAsync(string market, int count)
        {
            var json = await CallApiWithAuthAsync($"/v1/candles/minutes/1?market={market}&count={count}");
            if (IsError(json)) return new List<CommonCandle>();

            var dtos = JsonSerializer.Deserialize<List<UpbitCandleDto>>(json);
            return dtos?.Select(d => new CommonCandle
            {
                Time = d.CandleDateTimeKst,
                Open = d.OpeningPrice,
                High = d.HighPrice,
                Low = d.LowPrice,
                Close = d.TradePrice,
                Volume = d.CandleAccTradeVolume
            }).ToList() ?? new List<CommonCandle>();
        }

        public async Task<List<AssetItem>> GetAccountsAsync()
        {
            string json = await GetAccountsJsonAsync();
            if (IsError(json)) return new List<AssetItem>();

            var dtos = JsonSerializer.Deserialize<List<UpbitAccountDto>>(json);
            return dtos?.Select(d => new AssetItem
            {
                Exchange = "Upbit",
                Symbol = d.Currency,
                TotalInventory = Convert.ToDouble(d.Balance),
                AvgBuyPrice = Convert.ToDouble(d.AvgBuyPrice)
            }).ToList() ?? new List<AssetItem>();
        }

        public async Task<string> GetAccountsJsonAsync() => await CallApiWithAuthAsync("/v1/accounts");

        public async Task<string> GetOpenOrdersJsonAsync() => await CallApiWithAuthAsync("/v1/orders?state=wait");



        private async Task<string> CallApiWithAuthAsync(string path)
        {
            try
            {
                string queryString = path.Contains("?") ? path.Split('?')[1] : "";
                string jwtToken = _authenticator.CreateJwtToken(queryString);

                using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, path))
                {
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
                    var res = await _http.SendAsync(httpRequest);
                    string content = await res.Content.ReadAsStringAsync();

                    if (!res.IsSuccessStatusCode)
                    {
                        string cleanMsg = ParseErrorMessage(content);

                        if (content.Contains("no_authorization_ip"))
                        {
                            string myIp = await GetPublicIpAsync();
                            cleanMsg = $"{cleanMsg} (내 IP: {myIp})";
                        }

                        if (_lastApiError != cleanMsg)
                        {
                            _lastApiError = cleanMsg;
                            Logger.Log($"[Upbit API Error] {cleanMsg}", "ERROR");
                        }

                        // ⭐ 중요: 단순히 AUTH_FAILED 대신 에러 메시지 자체를 반환하거나 
                        // 앞에 특수한 접두사를 붙여 구분합니다.
                        return $"ERROR_MSG:{cleanMsg}";
                    }

                    _lastApiError = "";
                    return content;
                }
            }
            catch (Exception ex)
            {
                return $"ERROR_MSG:{ex.Message}";
            }
        }

        // ⭐ 내 공인 IP를 가져오는 헬퍼 메서드 (반드시 추가)
        private async Task<string> GetPublicIpAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                return await client.GetStringAsync("https://api.ipify.org");
            }
            catch
            {
                return "IP 확인 불가";
            }
        }

        private string ParseErrorMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    string message = errorElement.TryGetProperty("message", out var m) ? m.GetString() : "";

                    // ⭐ 핵심: 메시지에 IP 주소가 포함되어 있다면(업비트 에러 특징), 
                    // 정규식이 찾을 수 있도록 원문을 뒤에 살짝 붙여줍니다.
                    if (json.Contains("no_authorization_ip"))
                    {
                        return $"{message} (Raw: {json})";
                    }
                    return message;
                }
                return json;
            }
            catch
            {
                return json.Replace("{", "").Replace("}", "").Replace("\"", "").Trim();
            }
        }

        private bool IsError(string res) => res == "AUTH_FAILED" || res == "ERROR" || string.IsNullOrEmpty(res);
    }
}