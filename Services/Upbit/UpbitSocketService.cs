using ScottPlot;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Core;

namespace Upbit_Manager.Services.Upbit
{
    /// <summary>
    /// 업비트 웹소켓을 통해 실시간 체결 데이터 및 차트용 캔들 데이터를 제공하는 서비스입니다.
    /// </summary>
    public class UpbitSocketService : ICandleProvider
    {
        private CancellationTokenSource? _wsCts;
        private readonly Uri _uri = new Uri("wss://api.upbit.com/websocket/v1");

        // 실시간 체결 정보 이벤트 (가격, 거래량, 체결종류, 마켓코드)
        // ⭐ [활용방안]: 체결 시 사운드 재생, 실시간 체결창 업데이트, 자동매매 엔진의 틱 데이터 입력
        public event Action<double, double, string, string>? OnTradeUpdated;

        // ICandleProvider 인터페이스 구현

        // ⭐ [활용방안]: 차트 캔들 실시간 드로잉
        public event Action<OHLC>? OnCandleUpdated;

        // ⭐ [활용방안]: 연결 끊김 시 UI에 '재연결 중' 메시지 표시 및 타이머 일시 정지
        public event Action<bool>? OnConnectionStatusChanged;

        private OHLC? _currentCandle;
        private string? _activeMarket;

        public async Task RunLoopAsync(string[] markets)
        {
            if (markets == null || markets.Length == 0) return;

            _activeMarket = markets[0];
            Stop();
            _wsCts = new CancellationTokenSource();

            while (!_wsCts.Token.IsCancellationRequested)
            {
                try
                {
                    await StartWebSocketAsync(markets, _wsCts.Token);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Upbit WS] Reconnecting... {ex.Message}");
                }

                if (!_wsCts.Token.IsCancellationRequested)
                    await Task.Delay(5000);
            }
        }

        private async Task StartWebSocketAsync(string[] markets, CancellationToken ct)
        {
            using ClientWebSocket socket = new();
            socket.Options.SetBuffer(65536, 65536);

            await socket.ConnectAsync(_uri, ct);

            // 구독 메시지 (ticker가 아닌 trade 타입 사용)
            var subMsg = new object[]
            {
                new { ticket = "UPBIT_MANAGER_CLIENT" },
                new { type = "trade", codes = markets }
            };

            var msgBytes = JsonSerializer.SerializeToUtf8Bytes(subMsg);
            await socket.SendAsync(new ArraySegment<byte>(msgBytes), WebSocketMessageType.Text, true, ct);

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

                ParseAndNotify(ms.ToArray());
            }
        }

        private void ParseAndNotify(byte[] data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                if (root.TryGetProperty("code", out var codeProp))
                {
                    string market = codeProp.GetString() ?? "";
                    double price = root.GetProperty("trade_price").GetDouble();
                    double vol = root.GetProperty("trade_volume").GetDouble();
                    string side = root.GetProperty("ask_bid").GetString() ?? "BID";
                    long timestamp = root.GetProperty("trade_timestamp").GetInt64();

                    if (price <= 0) return;

                    // 1. 실시간 가격 정보 전파
                    OnTradeUpdated?.Invoke(price, vol, side, market);

                    // 2. 활성 마켓(차트 표시 중인 마켓)의 캔들 처리
                    if (market == _activeMarket)
                    {
                        ProcessCandleData(price, vol, timestamp);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WS Parse Error] {ex.Message}");
            }
        }

        private void ProcessCandleData(double price, double volume, long timestampMs)
        {
            var time = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).LocalDateTime;
            var minuteTime = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0);

            // OHLC는 struct이므로 Nullable 체크 시 패턴 매칭 활용
            if (_currentCandle is not OHLC current || current.DateTime != minuteTime)
            {
                // 새 분봉 시작
                _currentCandle = new OHLC(price, price, price, price, minuteTime, TimeSpan.FromMinutes(1));
            }
            else
            {
                // 기존 분봉 업데이트
                double high = Math.Max(current.High, price);
                double low = Math.Min(current.Low, price);
                _currentCandle = new OHLC(current.Open, high, low, price, minuteTime, TimeSpan.FromMinutes(1));
            }

            // 이벤트 발생 (Value를 통해 전달)
            if (_currentCandle.HasValue)
            {
                OnCandleUpdated?.Invoke(_currentCandle.Value);
            }
        }

        public void Stop()
        {
            _wsCts?.Cancel();
            _wsCts = null;
            _currentCandle = null;
        }

        public async Task StartAsync(string market, CancellationToken ct)
        {
            await RunLoopAsync(new[] { market });
        }
    }
}