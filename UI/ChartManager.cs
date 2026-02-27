// UI/ChartManager.cs

using ScottPlot;
using ScottPlot.WinForms;
using Upbit_Manager.Models.Common;
using Upbit_Manager.UI.Series;
using UpbitManager.Models.Upbit;

namespace Upbit_Manager.UI
{
    /// <summary>
    /// ScottPlot 차트 전담 관리자.
    /// 모든 시리즈는 _seriesList에 IChartSeries 구현체로 등록하고,
    /// UpdateUI()가 자동으로 순회하며 렌더링합니다.
    /// 새 시리즈 추가 = 구현 클래스 1개 + _seriesList 한 줄 추가.
    /// </summary>
    public class ChartManager
    {
        // ─── ScottPlot 플롯 객체 ──────────────────────────────────────
        private readonly FormsPlot _formsPlot;
        private ScottPlot.Plot _candlePlot;  // 상단: 가격/지표
        private ScottPlot.Plot _volumePlot;  // 하단: 거래량

        // ─── 단일 락 객체 (데드락 방지) ───────────────────────────────
        private readonly object _dataLock = new();

        // ─── 시리즈 레지스트리 ────────────────────────────────────────
        /// <summary>
        /// 모든 시리즈를 여기에만 등록합니다.
        /// 순서 = 체크리스트 표시 순서 = 렌더링 순서
        /// </summary>
        private readonly List<IChartSeries> _seriesList;

        // 자주 쓰는 시리즈는 직접 참조 (캐스팅 비용 절감)
        private readonly UpbitCandleSeries _upbitCandle;
        private readonly UpbitVolumeSeries _upbitVolume;
        private readonly UpbitUsdtLineSeries _upbitUsdtLine;
        private readonly UpbitOpenOrderSeries _upbitOpenOrder;
        private readonly BinancePriceLineSeries _binanceLine;

        // ─── 축 제어 상태 ─────────────────────────────────────────────
        private bool _isAutoScroll = true;
        private bool _isYAxisLocked = false;
        private double _zoomedSpan = 0;

        // concurrent queue for incoming ticks (produced by websocket thread)
        private readonly System.Collections.Concurrent.ConcurrentQueue<TradeTick> _tickQueue = new();

        private const string FONT = "Malgun Gothic";

        // ─── 생성자 ───────────────────────────────────────────────────
        public ChartManager(FormsPlot formsPlot)
        {
            _formsPlot = formsPlot;

            // 시리즈 인스턴스 생성 및 등록
            _upbitCandle = new UpbitCandleSeries();
            _upbitVolume = new UpbitVolumeSeries();
            _upbitUsdtLine = new UpbitUsdtLineSeries();
            _upbitOpenOrder = new UpbitOpenOrderSeries();
            _binanceLine = new BinancePriceLineSeries();

            _seriesList = new List<IChartSeries>
            {
                _upbitCandle,
                _upbitVolume,
                _upbitOpenOrder,
                _binanceLine,
                _upbitUsdtLine,
                // ↑ 새 시리즈는 여기에만 추가
            };

            // DefaultOn 값으로 초기 표시 상태 설정
            foreach (var s in _seriesList)
                s.IsVisible = s.DefaultOn;

            SetupMultiplot();
            SetupMouseInteraction();

            // UI 갱신 타이머 (100ms 주기, 데이터 수집과 독립)
            var uiTimer = new System.Windows.Forms.Timer { Interval = 100 };
            uiTimer.Tick += (s, e) =>
            {
                if (_formsPlot.IsHandleCreated)
                    UpdateUI();
            };
            uiTimer.Start();
        }

        // enqueue tick from background thread (no heavy locking)
        public void EnqueueTick(double price, double vol, string side)
        {
            _tickQueue.Enqueue(new TradeTick { Price = price, Volume = vol, Side = side, Time = DateTime.Now });
        }

        // ─── 시리즈 목록 공개 (Form의 체크리스트 초기화에 사용) ────────
        /// <summary>Form1에서 CheckedListBox를 초기화할 때 사용합니다.</summary>
        public IReadOnlyList<IChartSeries> SeriesList => _seriesList;

        // ─── 시리즈 표시 토글 ────────────────────────────────────────
        /// <summary>
        /// 체크리스트 항목 체크/해제 시 호출됩니다.
        /// MainController를 거쳐 전달됩니다.
        /// </summary>
        public void SetSeriesVisible(ExchangeSource source, SeriesType type, bool visible)
        {
            var series = _seriesList.FirstOrDefault(s => s.Source == source && s.Type == type);
            if (series == null) return;

            series.IsVisible = visible;

            // 바이낸스 선을 끌 때 버퍼 정리
            if (!visible && source == ExchangeSource.Binance && type == SeriesType.PriceLine)
                _binanceLine.Clear();
        }

        // ─── 데이터 업데이트 (외부 → 시리즈 내부 버퍼로 전달) ─────────

        /// <summary>초기 데이터 로드 (종목 변경 시 호출)</summary>
        public void InitializeWithData(string market,
                                       List<CommonCandle> candles,
                                       List<(DateTime Time, double Price)> binanceHistory = null,
                                       List<(DateTime Time, double Price)> upbitUsdtHistory = null)
        {
            lock (_dataLock)
            {
                // 각 시리즈 버퍼 초기화
                foreach (var s in _seriesList)
                    s.Clear();

                // 업비트 초기 데이터 전달
                _upbitCandle.UpdateData(candles ?? new());
                _upbitVolume.UpdateData(candles ?? new());
                // Upbit KRW-USDT 라인(김프 관찰용) 업데이트
                if (upbitUsdtHistory != null)
                    _upbitUsdtLine.UpdateData(upbitUsdtHistory);

                // 바이낸스 히스토리 전달
                if (binanceHistory != null)
                    _binanceLine.UpdateData(binanceHistory);

                // 종목 변경 시 축 상태 초기화
                _isAutoScroll = true;
                _isYAxisLocked = false;
                _zoomedSpan = 0;
            }

            _candlePlot.Title($"{market} Chart");
            ApplyAxisLimits();
            UpdateUI();
        }

        /// <summary>실시간 업비트 체결 데이터 업데이트</summary>
        public void UpdateRealtime(double price, double vol, string side)
        {
            var payload = new UpbitRealtimePayload(price, vol, side);
            lock (_dataLock)
            {
                _upbitCandle.UpdateData(payload);
                _upbitVolume.UpdateData(payload);
            }
        }

        /// <summary>실시간 바이낸스 가격 업데이트 (항상 수집, 표시는 IsVisible로 제어)</summary>
        public void UpdateBinancePrice(double krwPrice)
        {
            lock (_dataLock)
                _binanceLine.UpdateData(krwPrice);
        }

        /// <summary>바이낸스 히스토리 병합 (초기 로드 시 호출)</summary>
        public void UpdateBinanceHistory(List<(DateTime Time, double Price)> history)
        {
            if (history == null || history.Count == 0) return;
            lock (_dataLock)
                _binanceLine.UpdateData(history);
        }

        // ChartManager.cs 클래스 내부 적당한 곳에 추가

        /// <summary>업비트 KRW-USDT 실시간 가격 업데이트</summary>
        public void UpdateUsdtPrice(double price)
        {
            lock (_dataLock)
            {
                // 직접 참조 중인 _upbitUsdtLine에 데이터를 전달합니다.
                _upbitUsdtLine.UpdateData(price);
            }
        }


        /// <summary>미체결 주문 목록 업데이트</summary>
        public void UpdateMyOrders(List<UpbitOpenOrder> orders)
        {
            lock (_dataLock)
                _upbitOpenOrder.UpdateData(orders ?? new());
        }

        // ─── UI 렌더링 ────────────────────────────────────────────────

        /// <summary>
        /// 등록된 시리즈를 순회하며 자동 렌더링합니다.
        /// IsVisible == true인 시리즈만 Render()를 호출합니다.
        /// </summary>
        public void UpdateUI()
        {
            if (_formsPlot.InvokeRequired)
            {
                _formsPlot.Invoke(new Action(UpdateUI));
                return;
            }
            // 1) 드레인 큐: 웹소켓 스레드에서 들어온 틱을 한꺼번에 시리즈로 반영
            var ticks = new List<TradeTick>();
            while (_tickQueue.TryDequeue(out var t)) ticks.Add(t);

            if (ticks.Count > 0)
            {
                lock (_dataLock)
                {
                    foreach (var tt in ticks)
                    {
                        var payload = new UpbitRealtimePayload(tt.Price, tt.Volume, tt.Side);
                        _upbitCandle.UpdateData(payload);
                        // determine whether current candle is rising by inspecting candle buffer (fallback)
                        var candleBuf = _upbitCandle.GetBuffer();
                        bool isRising = false;
                        if (candleBuf.Count > 0)
                        {
                            var last = candleBuf[candleBuf.Count - 1];
                            isRising = last.Close >= last.Open;
                        }
                        // pass both side and isRising as tuple; series will prefer explicit side if available
                        _upbitVolume.UpdateData((tt.Price, tt.Volume, tt.Side, isRising));
                    }
                }
            }

            lock (_dataLock)
            {
                // 캔들 데이터가 없으면 렌더링 스킵
                if (_upbitCandle.GetBuffer().Count == 0) return;

                _candlePlot.Clear();
                _volumePlot.Clear();

                // IsVisible인 시리즈만 순서대로 렌더링
                foreach (var series in _seriesList.Where(s => s.IsVisible))
                {
                    series.Render(_candlePlot, _volumePlot);
                }

                ApplyAxisLimits();
                ConfigureTicks(_candlePlot);
                ConfigureTicks(_volumePlot);

                _formsPlot.Refresh();
            }
        }

        // ─── 축 범위 계산 ─────────────────────────────────────────────

        private void ApplyAxisLimits()
        {
            double nowOA = DateTime.Now.ToOADate();

            if (_isAutoScroll)
            {
                // X축: 현재 시간을 우측 끝에 고정
                double span = _zoomedSpan > 0 ? _zoomedSpan : TimeSpan.FromMinutes(200).TotalDays;
                double rightMargin = TimeSpan.FromMinutes(0.5).TotalDays;
                _candlePlot.Axes.Bottom.Range.Set(nowOA - span, nowOA + rightMargin);

                // Y축: 화면에 보이는 데이터의 가격 범위를 모든 시리즈에서 수집
                if (!_isYAxisLocked)
                {
                    double minOA = _candlePlot.Axes.Bottom.Range.Min;
                    double maxOA = _candlePlot.Axes.Bottom.Range.Max;

                    double high = double.MinValue;
                    double low = double.MaxValue;

                    // IsVisible인 시리즈에서 가격 범위 수집
                    foreach (var series in _seriesList.Where(s => s.IsVisible))
                    {
                        var range = series.GetPriceRange(minOA, maxOA);
                        if (range == null) continue;
                        high = Math.Max(high, range.Value.Max);
                        low = Math.Min(low, range.Value.Min);
                    }

                    if (high > double.MinValue && low < double.MaxValue)
                    {
                        double padding = (high - low) * 0.15;
                        if (padding == 0) padding = high * 0.002;
                        _candlePlot.Axes.Left.Range.Set(low - padding, high + padding);
                    }
                }
            }
            else
            {
                // 수동 모드: 너무 미래로 이동하면 자동 리셋
                if (_candlePlot.Axes.Bottom.Range.Min > nowOA + TimeSpan.FromMinutes(5).TotalDays)
                    ResetZoom();
            }

            // 하단 거래량 Y축 독립 조정
            double minX = _candlePlot.Axes.Bottom.Range.Min;
            double maxX = _candlePlot.Axes.Bottom.Range.Max;

            var volBuffer = _upbitVolume.GetBuffer();
            var timeBuffer = _upbitVolume.GetTimeBuffer();

            var visibleVols = timeBuffer
                .Select((t, i) => new { OA = t.ToOADate(), Vol = volBuffer[i] })
                .Where(x => x.OA >= minX && x.OA <= maxX)
                .Select(x => x.Vol)
                .ToList();

            if (visibleVols.Any())
            {
                double maxVol = visibleVols.Max();
                _volumePlot.Axes.Left.Range.Set(0, maxVol > 0 ? maxVol * 1.15 : 10);
            }

            // 하단 X축을 상단과 동기화
            _volumePlot.Axes.Bottom.Range.Set(
                _candlePlot.Axes.Bottom.Range.Min,
                _candlePlot.Axes.Bottom.Range.Max);
        }

        private void ConfigureTicks(ScottPlot.Plot plot)
        {
            var dtGen = new ScottPlot.TickGenerators.DateTimeAutomatic();
            dtGen.LabelFormatter = dt => dt.ToString("HH:mm:ss");
            plot.Axes.Bottom.TickGenerator = dtGen;
        }

        // ─── 마우스 인터랙션 ──────────────────────────────────────────

        private void SetupMouseInteraction()
        {
            _formsPlot.MouseDown += (s, e) =>
            {
                _isAutoScroll = false;
                _isYAxisLocked = true;
            };

            _formsPlot.MouseUp += (s, e) =>
            {
                // 우측 끝 근처면 자동 스크롤 재개
                double currentMax = _candlePlot.Axes.Bottom.Range.Max;
                double nowOA = DateTime.Now.ToOADate();
                if (currentMax >= nowOA - TimeSpan.FromMinutes(2).TotalDays)
                {
                    _isAutoScroll = true;
                    _isYAxisLocked = false;
                }
            };

            _formsPlot.MouseWheel += (s, e) => { _isAutoScroll = false; };
            _formsPlot.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right) ResetZoom();
            };
        }

        // ─── Multiplot 초기화 ─────────────────────────────────────────

        private void SetupMultiplot()
        {
            _formsPlot.Multiplot.AddPlots(2);
            _candlePlot = _formsPlot.Multiplot.Subplots.GetPlot(0);
            _volumePlot = _formsPlot.Multiplot.Subplots.GetPlot(1);

            _formsPlot.Multiplot.Layout = new TwoRowLayout(0.75f);

            _candlePlot.Layout.Fixed(new PixelPadding(85, 70, 20, 45));
            _volumePlot.Layout.Fixed(new PixelPadding(85, 70, 10, 8));

            InitAxisFormat(_candlePlot, true);
            InitAxisFormat(_volumePlot, false);

            SetupPlotStyle(_candlePlot, "Chart", true);
            SetupPlotStyle(_volumePlot, "", false);
        }

        private void InitAxisFormat(ScottPlot.Plot plot, bool showLabels)
        {
            plot.Axes.DateTimeTicksBottom();
            var dtGen = new ScottPlot.TickGenerators.DateTimeAutomatic();
            dtGen.LabelFormatter = dt => dt.ToString("HH:mm:ss");
            plot.Axes.Bottom.TickGenerator = dtGen;
            if (!showLabels)
                plot.Axes.Bottom.TickLabelStyle.IsVisible = false;
        }

        private void SetupPlotStyle(ScottPlot.Plot plot, string title, bool showYLabel)
        {
            plot.Title(title);
            plot.Axes.Title.Label.FontName = FONT;
            if (showYLabel) plot.YLabel("Price (KRW)");
            plot.FigureBackground.Color = Colors.WhiteSmoke;
            plot.Grid.MajorLineColor = Colors.LightGray.WithAlpha(0.5);
        }

        // ─── 공개 유틸리티 ────────────────────────────────────────────

        /// <summary>우측 끝(실시간)으로 이동하고 자동 스크롤 재개</summary>
        public void FollowRealtime()
        {
            lock (_dataLock)
            {
                if (_zoomedSpan <= 0)
                {
                    _zoomedSpan = _candlePlot.Axes.Bottom.Range.Span;
                    if (_zoomedSpan <= 0)
                        _zoomedSpan = TimeSpan.FromMinutes(20).TotalDays;
                }
                _isAutoScroll = true;
                _isYAxisLocked = false;
            }
            UpdateUI();
        }

        /// <summary>확대/축소 및 스크롤 상태를 기본값으로 리셋</summary>
        public void ResetZoom()
        {
            _isAutoScroll = true;
            _isYAxisLocked = false;
            _zoomedSpan = 0;
        }

        /// <summary>바이낸스 버퍼만 즉시 비우기 (체크 해제 시 호출)</summary>
        public void ClearBinanceSeries() => _binanceLine.Clear();
    }

    // ─── 레이아웃 헬퍼 ────────────────────────────────────────────────

    /// <summary>멀티플롯을 상/하 두 행으로 분할하는 레이아웃</summary>
    public class TwoRowLayout : ScottPlot.IMultiplotLayout
    {
        private readonly float _topFraction;
        public TwoRowLayout(float topFraction) { _topFraction = topFraction; }

        public PixelRect[] GetSubplotRectangles(SubplotCollection subplots, PixelRect figureRect)
        {
            PixelRect[] rects = new PixelRect[subplots.Count];
            float topH = figureRect.Height * _topFraction;
            float bottomH = figureRect.Height - topH;

            rects[0] = new PixelRect(new PixelSize(figureRect.Width, topH))
                           .WithDelta(figureRect.Left, figureRect.Top);
            if (subplots.Count > 1)
                rects[1] = new PixelRect(new PixelSize(figureRect.Width, bottomH))
                               .WithDelta(figureRect.Left, figureRect.Top + topH);

            for (int i = 2; i < subplots.Count; i++)
                rects[i] = new PixelRect(new PixelSize(figureRect.Width, 0))
                               .WithDelta(figureRect.Left, figureRect.Top);

            return rects;
        }
    }
}