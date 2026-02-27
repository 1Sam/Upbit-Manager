using System.Text.Json;
using Upbit_Manager.Core;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Services.Common;
using Upbit_Manager.UI;

namespace Upbit_Manager.Controllers
{
    public class MainController
    {
        public Action<double> OnExchangeRateUpdated; // 환율이 업데이트될 때 실행될 연결 고리

        private readonly IRestService _rest;
        private readonly ExchangeRateService _rateService; // ✅ 새로 추가된 전담 서비스

        private readonly AccountManager _accountManager;
        private readonly ChartManager _chartManager;
        private string _selectedMarket = "KRW-ADA";

        // 생성자에서 ExchangeRateService를 함께 주입받습니다.
        public MainController(IRestService rest, ExchangeRateService rateService, AccountManager accountManager, ChartManager chartManager)
        {
            _rest = rest;
            _rateService = rateService;
            _accountManager = accountManager;
            _chartManager = chartManager;
        }

        public async Task InitializeProgram()
        {
            await RefreshAssets();
            await ChangeMarket(_selectedMarket);
        }

        // 바이넌스를 위해 코인 코드 전달
        public string GetSelectedMarket() => _selectedMarket;

        public async Task ChangeMarket(string market)
        {
            _selectedMarket = market.Trim().ToUpper();

            // 1. 최신 환율 정보 가져오기
            double rate = 1450.0;
            try
            {
                rate = await _rateService.GetUsdToKrwAsync();

                // ✅ [추가] 가져온 환율을 UI 라벨에 표시하라고 신호 보냄
                OnExchangeRateUpdated?.Invoke(rate);
            }
            catch
            {
                // 실패해도 기본값으로 신호 보냄
                OnExchangeRateUpdated?.Invoke(rate);
            }

            // 2. 업비트 과거 캔들 가져오기
            var candles = await _rest.GetCandlesAsync(_selectedMarket, 200);

            // 3. 바이낸스 과거 시세 가져오기
            List<(DateTime Time, double Price)> binanceHistory = await GetBinanceHistoryAsync(_selectedMarket, rate);
            // 4. Upbit KRW-USDT 히스토리 가져오기 (김프 관찰용)
            List<(DateTime Time, double Price)> upbitUsdtHistory = new();
            try
            {
                var usdtCandles = await _rest.GetCandlesAsync("KRW-USDT", 200);
                if (usdtCandles != null && usdtCandles.Any())
                    upbitUsdtHistory = usdtCandles.Select(c => (c.Time, c.Close)).ToList();
            }
            catch { /* 실패해도 차트는 그립니다 */ }

            // 5. 차트 초기화 (Upbit KRW-USDT 히스토리 포함)
            _chartManager.InitializeWithData(_selectedMarket, candles, binanceHistory, upbitUsdtHistory);

            // 4. 차트 초기화
            _chartManager.InitializeWithData(_selectedMarket, candles, binanceHistory);
        }

        // 인수를 2개(마켓, 환율) 받도록 수정 완료
        public async Task<List<(DateTime Time, double Price)>> GetBinanceHistoryAsync(string upbitMarket, double rate)
        {
            try
            {
                // 1. 코인 심볼 추출 (예: KRW-BTC -> BTCUSDT)
                string coin = upbitMarket.Replace("KRW-", "");
                string url = $"https://api.binance.com/api/v3/klines?symbol={coin}USDT&interval=1m&limit=200";

                using HttpClient client = new HttpClient();
                var response = await client.GetStringAsync(url);

                // 2. JSON 파싱 및 데이터 변환
                using JsonDocument doc = JsonDocument.Parse(response);
                var history = doc.RootElement.EnumerateArray().Select(k =>
                {
                    // k[0]: Open Time (Unix Ms)
                    // k[4]: Close Price (String)
                    DateTime time = DateTimeOffset.FromUnixTimeMilliseconds(k[0].GetInt64()).LocalDateTime;

                    // 바이낸스 달러 가격에 환율을 곱해 KRW로 변환
                    double price = double.Parse(k[4].GetString()) * rate;
                    return (time, price);
                }).ToList();

                return history;
            }
            catch (Exception ex)
            {
                // 실패 시 빈 리스트 반환하여 차트 오류 방지
                return new List<(DateTime, double)>();
            }
        }

        public async Task RefreshAssets()
        {
            var assets = await _rest.GetAccountsAsync();
            _accountManager.UpdateAssets(assets);
        }

        public void HandleRealtimeTrade(double price, double vol, string side, string market)
        {
            // 1. 문자열 비교 시 공백과 대소문자 무시
            if (string.Equals(market?.Trim(), _selectedMarket?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                // 2. 업비트 실시간 차트 업데이트 (enqueue to avoid locking in websocket thread)
                _chartManager.EnqueueTick(price, vol, side);

                // 3. 자산 관리자에도 실시간가 전달
                _accountManager.UpdateCurrentPrice(_selectedMarket, price);
            }
        }

        public void ToggleSeries(ExchangeSource source, SeriesType type, bool isChecked)
        {
            switch (source)
            {
                case ExchangeSource.Upbit:
                    ToggleUpbitSeries(type, isChecked);
                    break;

                case ExchangeSource.Binance:
                    ToggleBinanceSeries(type, isChecked);
                    break;
            }
        }

        private void ToggleUpbitSeries(SeriesType type, bool isChecked)
        {
            switch (type)
            {
                case SeriesType.Candle:
                    _chartManager.SetSeriesVisible(ExchangeSource.Upbit, SeriesType.Candle, isChecked);
                    break;

                case SeriesType.Volume:
                    _chartManager.SetSeriesVisible(ExchangeSource.Upbit, SeriesType.Volume, isChecked);
                    break;

                case SeriesType.OpenOrder:
                    _chartManager.SetSeriesVisible(ExchangeSource.Upbit, SeriesType.OpenOrder, isChecked);
                    break;

                case SeriesType.RSI:
                    _chartManager.SetSeriesVisible(ExchangeSource.Upbit, SeriesType.RSI, isChecked);
                    break;
            }
        }

        private void ToggleBinanceSeries(SeriesType type, bool isChecked)
        {
            switch (type)
            {
                case SeriesType.PriceLine:
                    if (!isChecked)
                        _chartManager.ClearBinanceSeries();         // 꺼질 때 버퍼 정리
                    _chartManager.SetSeriesVisible(ExchangeSource.Binance, SeriesType.PriceLine, isChecked);
                    break;
            }
        }
    }
}