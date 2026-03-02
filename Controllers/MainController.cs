using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Upbit_Manager.Core;
using Upbit_Manager.Core.Alarms;
using Upbit_Manager.Core.Alarms.Conditions;
using Upbit_Manager.Core.Automation;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Services.Binance;
using Upbit_Manager.Services.Common;
using Upbit_Manager.Services.Upbit;
using Upbit_Manager.UI;
using Upbit_Manager.UI.Series;

namespace Upbit_Manager.Controllers
{
    public class MainController
    {
        // UI 연동 이벤트
        public Action<double>? OnExchangeRateUpdated;
        public Action<double, double>? OnVolumeStatsUpdated;

        // 서비스 및 매니저
        private readonly IRestService _upbitRest;
        private readonly ExchangeRateService _rateService;
        private readonly AccountManager _accountManager;
        private readonly ChartManager _chartManager;
        private readonly UpbitSocketService _upbitSocket;
        private readonly BinanceSocketService _binanceSocket;
        private readonly BinanceRestService _binanceRest;

        // 시스템 엔진
        private readonly AlarmManager _alarmManager;
        private readonly AlgoOrderManager _algoOrderManager; // [변경] 필드명을 클래스명에 맞춰 변경

        // 상태 변수
        private string _selectedMarket = "KRW-ADA";
        private double _currentRate = 1450.0;
        private bool _isBinanceActive = false;
        private double _volAlarmMultiplier = 5.0;

        private UpbitCandleSeries? _currentUpbitCandleSeries;

        public MainController(IRestService upbitRest, ExchangeRateService rateService, AccountManager accountManager, ChartManager chartManager)
        {
            _upbitRest = upbitRest;
            _rateService = rateService;
            _accountManager = accountManager;
            _chartManager = chartManager;

            _upbitSocket = new UpbitSocketService();
            _binanceSocket = new BinanceSocketService();
            _binanceRest = new BinanceRestService();

            _alarmManager = new AlarmManager();

            // AlgoOrderManager 초기화 (내부적으로 필요한 OrderManager가 있다면 생성자 주입)
            // 만약 AlgoOrderManager 생성자가 OrderManager를 요구하도록 수정했다면 아래와 같이 작성합니다.
             var orderManager = new OrderManager(upbitRest);
            _algoOrderManager = new AlgoOrderManager(orderManager);
            //_algoOrderManager = new AlgoOrderManager();

            // 1. 웹소켓 실시간 체결 데이터 처리
            _upbitSocket.OnTradeUpdated += HandleRealtimeTrade;

            // 2. 웹소켓 실시간 캔들(OHLC) 데이터 처리
            _upbitSocket.OnCandleUpdated += (candle) => {
                _chartManager.PushData(SeriesType.Candle, candle, ExchangeSource.Upbit);
                _chartManager.PushData(SeriesType.Volume, candle, ExchangeSource.Upbit);
            };

            _binanceSocket.OnPriceUpdated += HandleBinanceRealtime;

            _alarmManager.AlarmTriggered += (alarm, price, vol) =>
            {
                Logger.Log($"[알람 발생] {alarm.Name} | 가격: {price:N0} | 거래량: {vol:N0}");
            };
        }

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
            _currentUpbitCandleSeries = _chartManager.GetSeries<UpbitCandleSeries>(ExchangeSource.Upbit, SeriesType.Candle);

            double avgPrice = _accountManager.GetAvgBuyPrice(_selectedMarket);
            if (avgPrice > 0)
            {
                _chartManager.PushData(SeriesType.AvgPriceLine, avgPrice, ExchangeSource.Upbit);
            }

            SetupAlarms(avgPrice);

            _ = _upbitSocket.RunLoopAsync(new[] { _selectedMarket, "KRW-USDT" });

            if (_isBinanceActive)
            {
                await SyncBinanceHistory(_selectedMarket);
                await _binanceSocket.ConnectAsync(_selectedMarket);
            }

            UpdateVolumeThreshold();
        }

        #endregion

        #region [ 실시간 데이터 핸들링 ]

        private void HandleRealtimeTrade(double price, double vol, string side, string market)
        {
            string incomingMarket = market?.Trim().ToUpper() ?? "";
            _accountManager.UpdateCurrentPrice(incomingMarket, price);

            if (incomingMarket == _selectedMarket)
            {
                _chartManager.EnqueueTick(price, vol, side);
                _chartManager.UpdateCurrentPrice(price);
                _chartManager.PushData(SeriesType.PriceLine, (price, ExchangeSource.Upbit), ExchangeSource.Binance);

                if (_currentUpbitCandleSeries != null)
                {
                    double avgVol = _currentUpbitCandleSeries.GetAverageVolume(20);
                    double threshold = avgVol * _volAlarmMultiplier;

                    OnVolumeStatsUpdated?.Invoke(avgVol, threshold);
                    _chartManager.PushData(SeriesType.VolumeLimit, threshold, ExchangeSource.Upbit);

                    double currentAccumulatedVol = _currentUpbitCandleSeries.GetCurrentCandleVolume();
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

        #region [ 자동화 로직 ]

        /// <summary>
        /// AlgoOrderManager를 활용하여 그리드 매수를 실행합니다.
        /// </summary>
        public async Task ExecuteBatchPurchase(double startPrice)
        {
            try
            {
                Logger.Log($"[시스템] {_selectedMarket} | {startPrice:N0}원 기준 그리드 매수 알고리즘 가동...");

                // AlgoOrderManager 내부에 정의된 그리드 생성 로직 활용 (간격 2%, 4단계, 총액 100만 원 예시)
                var gridOrders = _algoOrderManager.GenerateGrid(startPrice, 2.0, 4, 1000000);

                // 생성된 그리드 주문들을 실행
                await _algoOrderManager.ExecuteGridOrders(_selectedMarket, gridOrders);
            }
            catch (Exception ex)
            {
                Logger.Log($"[오류] 알고리즘 주문 실행 실패: {ex.Message}");
            }
        }

        #endregion

        #region [ 설정 변경 및 알람 제어 ]

        public void SetVolumeMultiplier(double multiplier)
        {
            _volAlarmMultiplier = multiplier;
            UpdateVolumeThreshold();

            var volAlarm = _alarmManager.GetAlarms().OfType<RelativeVolumeAlarm>().FirstOrDefault();
            if (volAlarm != null)
            {
                volAlarm.Multiplier = multiplier;
            }
        }

        private void UpdateVolumeThreshold()
        {
            if (_currentUpbitCandleSeries != null)
            {
                double avgVol = _currentUpbitCandleSeries.GetAverageVolume(20);
                double threshold = avgVol * _volAlarmMultiplier;
                _chartManager.PushData(SeriesType.VolumeLimit, threshold, ExchangeSource.Upbit);
                OnVolumeStatsUpdated?.Invoke(avgVol, threshold);
            }
        }

        private void SetupAlarms(double avgBuyPrice)
        {
            _alarmManager.ClearAlarms();

            if (_currentUpbitCandleSeries != null)
            {
                var volAlarm = new RelativeVolumeAlarm(
                    () => _currentUpbitCandleSeries.GetAverageVolume(20),
                    _volAlarmMultiplier
                )
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
                    "보유종목 평단가 이탈 경고"
                )
                {
                    CooldownSeconds = 300
                };
                _alarmManager.AddAlarm(priceAlarm);
            }
        }

        #endregion

        #region [ 외부 서비스 연동 ]

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
                    var history = candles.Select(c => (c.Time, c.Close * _currentRate)).ToList();
                    _chartManager.PushData(SeriesType.PriceLine, history, ExchangeSource.Binance);
                }
            }
            catch (Exception ex) { Logger.Log($"Binance Sync Error: {ex.Message}"); }
        }

        public string GetSelectedMarket() => _selectedMarket;

        #endregion
    }
}