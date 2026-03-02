using System;
using System.Collections.Generic;
using System.Linq;
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

//각종 가격 데이터를 ChartManager에게 쏴줌.
namespace Upbit_Manager.Controllers
{
    public class MainController
    {
        // UI 연동 이벤트
        public Action<double>? OnExchangeRateUpdated;

        // ⭐ 추가: 거래량 통계 업데이트 이벤트 (평균거래량, 기준거래량)
        public Action<double, double>? OnVolumeStatsUpdated;

        // 의존성 주입 (Services)
        private readonly IRestService _upbitRest;
        private readonly ExchangeRateService _rateService;
        private readonly AccountManager _accountManager;
        private readonly ChartManager _chartManager;
        private readonly UpbitSocketService _upbitSocket;
        private readonly BinanceSocketService _binanceSocket;
        private readonly BinanceRestService _binanceRest;

        // 시스템 엔진 (Alarms & Automation)
        private readonly AlarmManager _alarmManager;
        private readonly GridOrderManager _gridOrderManager;

        // 상태 관리 변수
        private string _selectedMarket = "KRW-ADA";
        private double _currentRate = 1450.0;
        private bool _isBinanceActive = false;

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
            _gridOrderManager = new GridOrderManager();

            _upbitSocket.OnTradeUpdated += HandleRealtimeTrade;
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
                var accountJson = await _upbitRest.GetAccountsJsonAsync();
                if (accountJson.StartsWith("ERROR_MSG:")) return accountJson.Replace("ERROR_MSG:", "");

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

            if (_isBinanceActive) await SyncBinanceHistory(_selectedMarket);

            _ = _upbitSocket.RunLoopAsync(new[] { _selectedMarket, "KRW-USDT" });
            if (_isBinanceActive) await _binanceSocket.ConnectAsync(_selectedMarket);
        }

        #endregion

        #region [ 알람 로직 제어 ]

        private void SetupAlarms(double avgBuyPrice)
        {
            _alarmManager.ClearAlarms();

            if (_currentUpbitCandleSeries != null)
            {
                var volAlarm = new RelativeVolumeAlarm(
                    () => _currentUpbitCandleSeries.GetAverageVolume(20),
                    5.0
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

        #region [ 실시간 데이터 핸들링 ]

        private void HandleRealtimeTrade(double price, double vol, string side, string market)
        {
            string incomingMarket = market?.Trim().ToUpper() ?? "";
            _accountManager.UpdateCurrentPrice(incomingMarket, price);

            if (incomingMarket == _selectedMarket)
            {
                _chartManager.EnqueueTick(price, vol, side);
                _chartManager.UpdateCurrentPrice(price);

                // 1. 이미 다른 데이터(바이낸스/업비트 연동)를 여기서 쏘고 계십니다!
                _chartManager.PushData(SeriesType.PriceLine, (price, ExchangeSource.Upbit), ExchangeSource.Binance);

                if (_currentUpbitCandleSeries != null)
                {
                    double avgVol = _currentUpbitCandleSeries.GetAverageVolume(20);
                    // ⭐ 컴포넌트(UI)로부터 전달받아 저장해둔 배수(_volAlarmMultiplier)를 사용!
                    double threshold = avgVol * _volAlarmMultiplier;

                    // 2. UI 이벤트 발생 (라벨 업데이트용)
                    OnVolumeStatsUpdated?.Invoke(avgVol, threshold);

                    // ⭐ 3. 차트 시리즈용 데이터 전달 (이 줄이 빠져서 안 나왔던 겁니다)
                    _chartManager.PushData(SeriesType.VolumeLimit, threshold, ExchangeSource.Upbit);

                    // 알람 체크
                    double currentAccumulatedVol = _currentUpbitCandleSeries.GetCurrentCandleVolume();
                    _alarmManager.CheckAll(price, currentAccumulatedVol);
                }
            }
            else if (incomingMarket == "KRW-USDT")
            {
                // 4. USDT 가격도 여기서 쏘고 계시네요.
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

        #region [ 자동화 및 외부 연동 ]

        public async Task ExecuteBatchPurchase(double startPrice)
        {
            Logger.Log($"[시스템] {startPrice:N0}원 기준 그리드 일괄 매수 시퀀스 시작...");

            var gridOrders = new List<GridOrderItem>
            {
                new() { Price = startPrice,       Quantity = 5 },
                new() { Price = startPrice * 0.98, Quantity = 10 },
                new() { Price = startPrice * 0.96, Quantity = 15 },
                new() { Price = startPrice * 0.94, Quantity = 20 }
            };

            await _gridOrderManager.ExecuteBatchBuyLimitOrders(_selectedMarket, gridOrders);
        }

        #endregion

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



        private double _volAlarmMultiplier = 5.0; // 기본값

        // UI에서 배수를 변경했을 때 호출될 메서드
        public void SetVolumeMultiplier(double multiplier)
        {
            _volAlarmMultiplier = multiplier;
            // 변경 즉시 차트 선을 새로 그리기 위해 강제 업데이트 호출 가능
            // ⭐ 배수가 변경되었을 때, 다음 틱을 기다리지 않고 즉시 차트를 갱신하고 싶다면
            // 현재 계산된 마지막 평균값이 있을 때 PushData를 여기서 한 번 더 호출할 수도 있습니다.
            UpdateVolumeThreshold();
        }

        private void UpdateVolumeThreshold()
        {
            if (_currentUpbitCandleSeries != null)
            {
                double avgVol = _currentUpbitCandleSeries.GetAverageVolume(20);
                double threshold = avgVol * _volAlarmMultiplier;
                _chartManager.PushData(SeriesType.VolumeLimit, threshold, ExchangeSource.Upbit);
            }
        }

    }
}