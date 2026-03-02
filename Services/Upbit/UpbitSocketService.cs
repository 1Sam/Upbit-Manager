using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Upbit_Manager.Core;

namespace Upbit_Manager.Services.Upbit
{
    public class UpbitSocketService
    {
        private CancellationTokenSource? _wsCts;
        public event Action<double, double, string, string>? OnTradeUpdated;

        public async Task RunLoopAsync(string[] markets)
        {
            if (markets == null || markets.Length == 0) return;
            Stop();
            _wsCts = new CancellationTokenSource();

            while (!_wsCts.Token.IsCancellationRequested)
            {
                try { await StartWebSocketAsync(markets, _wsCts.Token); }
                catch (Exception ex) { Logger.Log($"[Upbit WS] Reconnecting... {ex.Message}"); }

                if (!_wsCts.Token.IsCancellationRequested) await Task.Delay(5000);
            }
        }

        private async Task StartWebSocketAsync(string[] markets, CancellationToken ct)
        {
            using ClientWebSocket socket = new();
            socket.Options.SetBuffer(65536, 65536);
            await socket.ConnectAsync(new Uri("wss://api.upbit.com/websocket/v1"), ct);

            // 구독 메시지
            var subMsg = new object[] { new { ticket = "CHART_CLIENT" }, new { type = "trade", codes = markets } };
            await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(subMsg), WebSocketMessageType.Text, true, ct);

            byte[] buffer = new byte[65536];

            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                byte[] data = ms.ToArray();
                _ = Task.Run(() => ParseAndNotify(data), ct);
            }
        }

        private void ParseAndNotify(byte[] data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                if (root.TryGetProperty("code", out var cProp))
                {
                    string market = cProp.GetString() ?? "";
                    double price = root.GetProperty("trade_price").GetDouble();
                    double vol = root.GetProperty("trade_volume").GetDouble();
                    string side = root.GetProperty("ask_bid").GetString() ?? "BID";

                    if (price > 0) OnTradeUpdated?.Invoke(price, vol, side, market);
                }
            }
            catch { }
        }

        public void Stop() => _wsCts?.Cancel();
    }
}