// ✅ [수정] Controllers/MainController.cs
// 🔧 OnOrderbookReceived 이중 등록 버그 수정 → 단일 핸들러로 통합

using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Upbit_Manager.Core;
using Upbit_Manager.Core.Alarms;
using Upbit_Manager.Core.Alarms.Conditions;
using Upbit_Manager.Core.Automation;
using Upbit_Manager.Core.Orderbook;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Services;
using Upbit_Manager.Services.Binance;
using Upbit_Manager.Services.Common;
using Upbit_Manager.Services.Upbit;
using Upbit_Manager.UI;
using Upbit_Manager.UI.Series;

namespace Upbit_Manager.Controllers
{
    /// <summary>
    /// 애플리케이션의 중앙 오케스트레이터
    ///
    /// 책임:
    /// - REST 초기 데이터 로딩
    /// - WebSocket 실시간 데이터 처리
    /// - MMF 오더북 데이터 수신 및 전달
    /// - ChartManager로 데이터 전달
    /// - 알람 및 자동매매 엔진 제어
    /// </summary>
    public class MainController
    {
        #region [ UI 이벤트 ]

        public Action<double>? OnExchangeRateUpdated;
        public Action<double, double>? OnVolumeStatsUpdated;
        public Action<bool>? OnMMFStatusChanged;

        #endregion

        #region [ 서비스 및 매니저 ]

        private readonly IRestService _upbitRest;
        private readonly ExchangeRateService _rateService;
        private readonly AccountManager _accountManager;
        private readonly ChartManager _chartManager;

        private readonly UpbitSocketService _upbitSocket;
        private readonly BinanceSocketService _binanceSocket;
        private readonly BinanceRestService _binanceRest;

        private readonly MMFBridgeService _mmfBridge;

        private readonly AlarmManager _alarmManager;
        private readonly AlgoOrderManager _algoOrderManager;

        #endregion

        #region [ 상태 변수 ]

        private string _selectedMarket = "KRW-ADA";
        private double _currentRate = 1450.0;
        private bool _isBinanceActive;
        private double _volAlarmMultiplier = 5.0;
        private bool _isMMFConnected;
        private bool _mmfStarted;

        private UpbitCandleSeries? _currentUpbitCandleSeries;

        #endregion

        private readonly OrderbookHeatmapEngine _heatmapEngine;

        // 🔥 HeatmapForm에 직접 전달 (ChartManager 경유 불필요)
        public Action<OrderbookSnapshot>? OnHeatmapSnapshot;

        public MainController(
            IRestService upbitRest,
            ExchangeRateService rateService,
            AccountManager accountManager,
            ChartManager chartManager, OrderbookHeatmapEngine heatmapEngine)
        {
            _upbitRest = upbitRest;
            _rateService = rateService;
            _accountManager = accountManager;
            _chartManager = chartManager;

            _upbitSocket = new UpbitSocketService();
            _binanceSocket = new BinanceSocketService();
            _binanceRest = new BinanceRestService();

            _alarmManager = new AlarmManager();
            _mmfBridge = new MMFBridgeService();

            var orderManager = new OrderManager(upbitRest);
            _algoOrderManager = new AlgoOrderManager(orderManager);

            // ✅ 내부에서 new() 하던 것 제거
            _heatmapEngine = heatmapEngine;

            _heatmapEngine.OnSpoofingDetected += e =>
            {
                Logger.Log($"[Spoofing 감지] {e.Side} | 가격: {e.Price:N0} | 잔량: {e.Volume:N0} | 지속: {e.Duration.TotalSeconds:F1}초");
            };

            _heatmapEngine.OnLargeOrderDetected += (price, volume, side) =>
            {
                Logger.Log($"[대량 호가] {side} | 가격: {price:N0} | 잔량: {volume:N0}");
            };

            RegisterSocketEvents();
            RegisterMMFEvents();
            RegisterAlarmEvents();
        }

        #region [ 이벤트 등록 ]

        private void RegisterSocketEvents()
        {
            _upbitSocket.OnTradeUpdated += HandleRealtimeTrade;

            _upbitSocket.OnCandleUpdated += candle =>
            {
                _chartManager.PushData(SeriesType.Candle, candle, ExchangeSource.Upbit);
                _chartManager.PushData(SeriesType.Volume, candle, ExchangeSource.Upbit);
            };

            _binanceSocket.OnPriceUpdated += HandleBinanceRealtime;
        }

        private void RegisterMMFEvents()
        {
            _mmfBridge.OnOrderbookReceived += (timestamp, units) =>
            {
                if (units.Length == 0) return;

                _chartManager.PushOrderbook(timestamp, units);

                var snapshot = new OrderbookSnapshot
                {
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime,
                    TimestampMs = timestamp,
                    Units = units,
                    TotalAskSize = units.Sum(u => u.AskSize),
                    TotalBidSize = units.Sum(u => u.BidSize)
                };

                // 🔥 ChartManager 대신 HeatmapForm으로 직접
                OnHeatmapSnapshot?.Invoke(snapshot);
            };

            _mmfBridge.OnConnectionStatusChanged += isConnected =>
            {
                _isMMFConnected = isConnected;
                OnMMFStatusChanged?.Invoke(isConnected);

                Logger.Log(
                    isConnected
                    ? "[시스템] 로컬 Collector(MMF) 오더북 모드 활성화"
                    : "[시스템] API 기반 데이터 모드 전환");
            };
        }

        private void RegisterAlarmEvents()
        {
            _alarmManager.AlarmTriggered += (alarm, price, vol) =>
            {
                Logger.Log($"[알람 발생] {alarm.Name} | 가격: {price:N0} | 거래량: {vol:N0}");
            };
        }

        #endregion

        #region [ 초기화 및 시장 변경 ]

        public async Task<string> InitializeProgram()
        {
            try
            {
                var assets = await _upbitRest.GetAccountsAsync();
                _accountManager.UpdateAssets(assets);

                await ChangeMarket(_selectedMarket);
                return "SUCCESS";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task ChangeMarket(string market)
        {
            _selectedMarket = market.Trim().ToUpper();

            _currentRate = await _rateService.GetUsdToKrwAsync();
            OnExchangeRateUpdated?.Invoke(_currentRate);

            var candles = await _upbitRest.GetCandlesAsync(_selectedMarket, 200);
            var usdtCandles = await _upbitRest.GetCandlesAsync("KRW-USDT", 200);
            var usdtHistory = usdtCandles.Select(c => (c.Time, c.Close)).ToList();

            _chartManager.InitializeWithData(_selectedMarket, candles, null, usdtHistory);
            _currentUpbitCandleSeries =
                _chartManager.GetSeries<UpbitCandleSeries>(ExchangeSource.Upbit, SeriesType.Candle);

            double avgPrice = _accountManager.GetAvgBuyPrice(_selectedMarket);
            if (avgPrice > 0)
                _chartManager.PushData(SeriesType.AvgPriceLine, avgPrice, ExchangeSource.Upbit);

            SetupAlarms(avgPrice);

            _ = _upbitSocket.RunLoopAsync(new[] { _selectedMarket, "KRW-USDT" });

            if (_isBinanceActive)
            {
                await SyncBinanceHistory(_selectedMarket);
                await _binanceSocket.ConnectAsync(_selectedMarket);
            }

            UpdateVolumeThreshold();

            // MMF는 프로그램 수명 동안 1회만 시작
            if (!_mmfStarted)
            {
                _mmfBridge.Start();
                _mmfStarted = true;
            }
        }

        public void Shutdown()
        {
            _mmfBridge.Dispose();
            _upbitSocket.Stop();
            _binanceSocket.DisconnectAsync().Wait();
        }

        #endregion

        #region [ 실시간 처리 ]

        private void HandleRealtimeTrade(double price, double vol, string side, string market)
        {
            string incomingMarket = market?.Trim().ToUpper() ?? "";
            _accountManager.UpdateCurrentPrice(incomingMarket, price);

            // 체결 가격 → 히트맵 엔진에 주입 (스푸핑 판별용)
            _heatmapEngine.RegisterTrade(price);

            if (incomingMarket == _selectedMarket)
            {
                _chartManager.EnqueueTick(price, vol, side);

                _chartManager.PushData(
                    SeriesType.PriceLine,
                    (price, ExchangeSource.Upbit),
                    ExchangeSource.Binance);

                if (_currentUpbitCandleSeries != null)
                {
                    double avgVol = _currentUpbitCandleSeries.GetAverageVolume(20);
                    double threshold = avgVol * _volAlarmMultiplier;

                    OnVolumeStatsUpdated?.Invoke(avgVol, threshold);
                    _chartManager.PushData(SeriesType.VolumeLimit, threshold, ExchangeSource.Upbit);

                    double currentAccumulatedVol =
                        _currentUpbitCandleSeries.GetCurrentCandleVolume();

                    _alarmManager.CheckAll(price, currentAccumulatedVol);
                }
            }
            else if (incomingMarket == "KRW-USDT")
            {
                _chartManager.PushData(SeriesType.PriceLine, price, ExchangeSource.Upbit);
            }
        }

        private void HandleBinanceRealtime(double usdPrice)
        {
            if (_isBinanceActive && _currentRate > 0)
            {
                double binanceKrw = usdPrice * _currentRate;
                _chartManager.PushData(SeriesType.PriceLine, binanceKrw, ExchangeSource.Binance);
            }
        }

        #endregion

        #region [ 자동매매 ]

        public async Task ExecuteBatchPurchase(double startPrice)
        {
            try
            {
                Logger.Log($"[시스템] {_selectedMarket} | {startPrice:N0}원 기준 그리드 매수 실행");

                var gridOrders =
                    _algoOrderManager.GenerateGrid(startPrice, 2.0, 4, 1_000_000);

                await _algoOrderManager.ExecuteGridOrders(_selectedMarket, gridOrders);
            }
            catch (Exception ex)
            {
                Logger.Log($"[오류] 알고리즘 주문 실패: {ex.Message}");
            }
        }

        #endregion

        #region [ 알람 및 설정 ]

        public void SetVolumeMultiplier(double multiplier)
        {
            _volAlarmMultiplier = multiplier;
            UpdateVolumeThreshold();

            var volAlarm =
                _alarmManager.GetAlarms().OfType<RelativeVolumeAlarm>().FirstOrDefault();

            if (volAlarm != null)
                volAlarm.Multiplier = multiplier;
        }

        private void UpdateVolumeThreshold()
        {
            if (_currentUpbitCandleSeries == null)
                return;

            double avgVol = _currentUpbitCandleSeries.GetAverageVolume(20);
            double threshold = avgVol * _volAlarmMultiplier;

            _chartManager.PushData(SeriesType.VolumeLimit, threshold, ExchangeSource.Upbit);
            OnVolumeStatsUpdated?.Invoke(avgVol, threshold);
        }

        private void SetupAlarms(double avgBuyPrice)
        {
            _alarmManager.ClearAlarms();

            if (_currentUpbitCandleSeries != null)
            {
                var volAlarm = new RelativeVolumeAlarm(
                    () => _currentUpbitCandleSeries.GetAverageVolume(20),
                    _volAlarmMultiplier)
                {
                    CooldownSeconds = 60,
                    IsDiscordNotify = true
                };

                _alarmManager.AddAlarm(volAlarm);
            }

            if (avgBuyPrice > 0)
            {
                var priceAlarm = new PriceThresholdAlarm(
                    avgBuyPrice,
                    PriceThresholdAlarm.PriceDirection.Below,
                    "보유종목 평단가 이탈 경고")
                {
                    CooldownSeconds = 300
                };

                _alarmManager.AddAlarm(priceAlarm);
            }
        }

        #endregion

        #region [ Binance 연동 ]

        public async Task ToggleBinanceService(bool isChecked)
        {
            _isBinanceActive = isChecked;

            if (isChecked)
            {
                await SyncBinanceHistory(_selectedMarket);
                await _binanceSocket.ConnectAsync(_selectedMarket);
            }
            else
            {
                await _binanceSocket.DisconnectAsync();
                _chartManager.PushData(SeriesType.PriceLine, double.NaN, ExchangeSource.Binance);
            }
        }

        public async Task SyncBinanceHistory(string upbitMarket)
        {
            try
            {
                var candles = await _binanceRest.GetCandlesAsync(upbitMarket, 200);

                if (candles != null && candles.Any())
                {
                    var history = candles
                        .Select(c => (c.Time, c.Close * _currentRate))
                        .ToList();

                    _chartManager.PushData(SeriesType.PriceLine, history, ExchangeSource.Binance);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Binance Sync Error: {ex.Message}");
            }
        }

        public string GetSelectedMarket() => _selectedMarket;

        #endregion
    }
}