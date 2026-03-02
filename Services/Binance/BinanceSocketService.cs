using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Upbit_Manager.Services.Binance
{
    public class BinanceSocketService
    {
        private ClientWebSocket? _socket;
        private CancellationTokenSource? _cts;
        public event Action<double>? OnPriceUpdated;

        public async Task ConnectAsync(string market)
        {
            await DisconnectAsync();
            _cts = new CancellationTokenSource();
            string symbol = market.Replace("KRW-", "").ToLower() + "usdt";
            Uri uri = new Uri($"wss://stream.binance.com:9443/ws/{symbol}@trade");

            _ = Task.Run(async () => {
                try
                {
                    using (_socket = new ClientWebSocket())
                    {
                        await _socket.ConnectAsync(uri, _cts.Token);
                        byte[] buffer = new byte[8192]; // 버퍼 넉넉히

                        while (_socket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                        {
                            var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                            if (result.MessageType == WebSocketMessageType.Close) break;

                            // ⭐ JsonSerializer 대신 JsonDocument 사용 (훨씬 빠름)
                            using var doc = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
                            if (doc.RootElement.TryGetProperty("p", out var pProp))
                            {
                                if (double.TryParse(pProp.GetString(), out double price))
                                {
                                    OnPriceUpdated?.Invoke(price);
                                }
                            }
                        }
                    }
                }
                catch(Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Binance Socket Error: {ex.Message}");
                }
            }, _cts.Token);
        }

        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            if (_socket != null)
            {
                try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { }
                _socket.Dispose(); _socket = null;
            }
        }
    }
}