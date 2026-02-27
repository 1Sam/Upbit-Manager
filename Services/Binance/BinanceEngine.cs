using System.Net.WebSockets;
using System.Text.Json;

namespace Upbit_Manager.Services
{
    public class BinanceEngine
    {
        private ClientWebSocket? _socket;
        private CancellationTokenSource? _cts;
        public event Action<double>? OnPriceUpdated; // 환산된 원화 가격 전달

        // 업비트 "KRW-ADA" -> 바이낸스 "adausdt" 변환기
        private string ConvertToBinanceSymbol(string upbitMarket)
            => upbitMarket.Replace("KRW-", "").ToLower() + "usdt";

        public async Task StartAsync(string upbitMarket, double exchangeRate)
        {
            await StopAsync();
            _cts = new CancellationTokenSource();
            string binanceSymbol = ConvertToBinanceSymbol(upbitMarket);
            string url = $"wss://stream.binance.com:9443/ws/{binanceSymbol}@trade";

            _ = Task.Run(async () =>
            {
                try
                {
                    using (_socket = new ClientWebSocket())
                    {
                        await _socket.ConnectAsync(new Uri(url), _cts.Token);
                        byte[] buffer = new byte[4096];

                        while (_socket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                        {
                            var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                            using var doc = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
                            var root = doc.RootElement;

                            // 바이낸스 trade 데이터 키: "p"는 가격(price)
                            if (root.TryGetProperty("p", out var priceProp))
                            {
                                double usdPrice = double.Parse(priceProp.GetString() ?? "0");
                                double krwPrice = usdPrice * exchangeRate; // 원화 환산
                                OnPriceUpdated?.Invoke(krwPrice);
                            }
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Binance Error: {ex.Message}"); }
            }, _cts.Token);
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();

            if (_socket != null)
            {
                // 소켓이 '열려있을 때만' 정상 종료 시도
                if (_socket.State == WebSocketState.Open ||
                    _socket.State == WebSocketState.CloseReceived ||
                    _socket.State == WebSocketState.CloseSent)
                {
                    try
                    {
                        await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"소켓 종료 중 예외 발생: {ex.Message}");
                    }
                }

                _socket.Dispose();
                _socket = null;
            }
        }
    }
}