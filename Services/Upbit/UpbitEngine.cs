using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Upbit_Manager.Core;
using Upbit_Manager.Interfaces;

namespace Upbit_Manager.Services
{
    public class UpbitEngine
    {
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private readonly AccountManager _accountManager;
        private readonly UpbitAuthenticator _authenticator;
        private CancellationTokenSource? _wsCts;

        public event Action<double, double, string, string>? OnTradeUpdated;

        public UpbitEngine(AccountManager accountManager)
        {
            _accountManager = accountManager;
            // 인증 객체 주입 (ApiConfig 참조)
            _authenticator = new UpbitAuthenticator(ApiConfig.AccessKey, ApiConfig.SecretKey);
        }

        #region [WebSocket Logic]
        public async Task RunWebSocketLoopAsync(string[] markets)
        {
            if (markets == null || markets.Length == 0) return;
            _wsCts = new CancellationTokenSource();

            while (!_wsCts.Token.IsCancellationRequested)
            {
                try { await StartWebSocketAsync(markets, _wsCts.Token); }
                catch (Exception ex) { Logger.Error(ex, "WebSocket Reconnecting..."); }

                if (!_wsCts.Token.IsCancellationRequested) await Task.Delay(5000);
            }
        }

        private async Task StartWebSocketAsync(string[] markets, CancellationToken ct)
        {
            using ClientWebSocket socket = new();
            // 수신 버퍼를 64KB로 넉넉하게 설정 (BTC와 같은 고빈도 종목 대응)
            socket.Options.SetBuffer(65536, 65536);

            await socket.ConnectAsync(new Uri(ApiConfig.WebSocketUrl), ct);

            // 구독 메시지 전송
            var subMsg = new object[] { new { ticket = "CHART_CLIENT" }, new { type = "trade", codes = markets } };
            await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(subMsg), WebSocketMessageType.Text, true, ct);

            byte[] buffer = new byte[65536];

            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                // [핵심] 메시지가 여러 조각으로 나누어 들어와도 EndOfMessage까지 모두 수집
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                // [성능 최적화] 데이터 처리는 별도 Task로 던져서 소켓 수신 루프가 멈추지 않게 함
                byte[] dataToProcess = ms.ToArray();
                _ = Task.Run(() =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(dataToProcess);
                        var root = doc.RootElement;

                        // 안전하게 프로퍼티 추출 (TryGet 시리즈 사용)
                        if (root.TryGetProperty("code", out var codeProp))
                        {
                            string market = codeProp.GetString() ?? "";

                            // 가격(trade_price) 추출: 정수/실수 모두 대응 가능하도록 처리
                            double price = 0;
                            if (root.TryGetProperty("trade_price", out var pProp))
                                price = pProp.GetDouble();

                            // 거래량(trade_volume) 추출
                            double vol = 0;
                            if (root.TryGetProperty("trade_volume", out var vProp))
                                vol = vProp.GetDouble();

                            // 매수/매도 구분
                            string side = "BID";
                            if (root.TryGetProperty("ask_bid", out var sProp))
                                side = sProp.GetString() ?? "BID";

                            // 데이터가 유효할 때만 이벤트 발생
                            if (price > 0)
                            {
                                _accountManager.UpdateCurrentPrice(market, price);
                                OnTradeUpdated?.Invoke(price, vol, side, market);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 개별 메시지 파싱 에러는 기록만 하고 루프는 유지
                        System.Diagnostics.Debug.WriteLine($"[Parsing Error] {ex.Message}");
                    }
                }, ct);
            }


        }

      
        public void StopWebSocket() => _wsCts?.Cancel();
        #endregion

        #region [Private API Wrapper]
        public async Task<string> GetAccountsAsync()
            => await CallPrivateApiAsync("/v1/accounts");

        private async Task<string> CallPrivateApiAsync(string urlPathWithQuery)
        {
            string jwtToken = _authenticator.CreateJwtToken(urlPathWithQuery);
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiConfig.RestApiBaseUrl}{urlPathWithQuery}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : string.Empty;
        }

        public async Task<(bool isSuccess, string message)> CheckAuthAsync()
        {
            var result = await GetAccountsAsync();
            return !string.IsNullOrEmpty(result) ? (true, "인증 성공") : (false, "인증 실패 (키를 확인하세요)");
        }
        #endregion
    }
}