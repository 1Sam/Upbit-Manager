using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Upbit_Manager.Models.Common;
using Upbit_Manager.UI.Series;
using Upbit_Manager.Models.Upbit;
using Upbit_Manager.Interfaces;

namespace Upbit_Manager.UI
{
    /// <summary>
    /// ScottPlot 5.1 기반의 멀티플롯 차트 관리자입니다.
    /// 가격 차트와 거래량 차트를 동기화하여 렌더링하며, 다양한 데이터 시리즈(IChartSeries)를 관리합니다.
    /// </summary>
    public class ChartManager
    {
        private double _lastPrice = 0;
        public static double LastTimestampOA { get; private set; }

        private readonly FormsPlot _formsPlot;
        private Plot _candlePlot;
        private Plot _volumePlot;

        private HorizontalLine _currentPriceLine;
        private readonly string _malgunFontName;
        private readonly object _dataLock = new();
        private readonly List<IChartSeries> _seriesList = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<TradeTick> _tickQueue = new();

        private bool _isAutoScroll = true;
        private bool _isYAxisLocked = false;

        // ─── 차트 뷰 설정 ──────────────────────────────────────────
        private double _fixedSpan = TimeSpan.FromMinutes(200).TotalDays; // 기본 200분 폭
        private readonly double _rightMarginSpan = TimeSpan.FromSeconds(30).TotalDays; // 우측 30초 여백
        private double _lastDataTimeOA = 0;

        public ChartManager(FormsPlot formsPlot)
        {
            _formsPlot = formsPlot;

            // 1. 폰트 설정 (한글 깨짐 방지)
            using (Font malgun = new Font("맑은 고딕", 12))
            {
                _malgunFontName = malgun.Name;
            }
            ScottPlot.Fonts.Default = _malgunFontName;

            // 2. 초기화 프로세스
            InitializeSeries();
            SetupMultiplot();
            SetupMouseInteraction();

            // 3. UI 갱신 타이머 (100ms)
            var uiTimer = new System.Windows.Forms.Timer { Interval = 100 };
            uiTimer.Tick += (s, e) => { if (_formsPlot.IsHandleCreated) UpdateUI(); };
            uiTimer.Start();
        }

        private void InitializeSeries()
        {
            _seriesList.Clear();
            // 리플렉션을 통해 프로젝트 내 모든 IChartSeries 구현체를 자동으로 찾아 등록합니다.
            var seriesTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => typeof(IChartSeries).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in seriesTypes)
            {
                if (Activator.CreateInstance(type) is IChartSeries s)
                {
                    s.IsVisible = s.DefaultOn;
                    _seriesList.Add(s);
                }
            }
        }

        public IReadOnlyList<IChartSeries> SeriesList => _seriesList;

        /// <summary>
        /// 외부에서 실시간 체결 정보를 큐에 삽입합니다.
        /// </summary>
        public void EnqueueTick(double price, double vol, string side)
        {
            _tickQueue.Enqueue(new TradeTick { Price = price, Volume = vol, Side = side, Time = DateTime.Now });
        }

        /// <summary>
        /// 특정 시리즈에 데이터를 직접 주입합니다.
        /// </summary>
        public void PushData(SeriesType type, object payload, ExchangeSource source = ExchangeSource.Upbit)
        {
            lock (_dataLock)
            {
                var targets = _seriesList.Where(s => s.Type == type && s.Source == source);
                foreach (var s in targets) s.UpdateData(payload);
            }
        }

        /// <summary>
        /// 차트 초기화 (과거 캔들 및 타 거래소 이력 데이터 로드)
        /// </summary>
        public void InitializeWithData(string market, List<CommonCandle> candles,
                                       List<(DateTime Time, double Price)> binanceHistory = null,
                                       List<(DateTime Time, double Price)> upbitUsdtHistory = null)
        {
            lock (_dataLock)
            {
                foreach (var s in _seriesList) s.Clear();

                foreach (var series in _seriesList)
                {
                    if (series.Source == ExchangeSource.Upbit && (series.Type == SeriesType.Candle || series.Type == SeriesType.Volume))
                        series.UpdateData(candles ?? new());
                    else if (series.Source == ExchangeSource.Binance && series.Type == SeriesType.PriceLine && binanceHistory != null)
                        series.UpdateData(binanceHistory);
                    else if (series.Source == ExchangeSource.Upbit && series.Type == SeriesType.PriceLine && series.Label.Contains("USDT") && upbitUsdtHistory != null)
                        series.UpdateData(upbitUsdtHistory);
                }

                _lastDataTimeOA = (candles != null && candles.Count > 0)
                    ? candles.Last().Time.ToOADate()
                    : DateTime.Now.ToOADate();

                ResetZoom();
            }
            _candlePlot.Title($"{market} Chart");
            UpdateUI();
        }

        private void ProcessTickQueue()
        {
            while (_tickQueue.TryDequeue(out var t))
            {
                _lastDataTimeOA = t.Time.ToOADate();
                var payload = new UpbitRealtimePayload(t.Price, t.Volume, t.Side);
                lock (_dataLock)
                {
                    foreach (var s in _seriesList) s.UpdateData(payload);
                }
            }
        }

        public void UpdateUI()
        {
            if (_formsPlot.InvokeRequired) { _formsPlot.Invoke(new Action(UpdateUI)); return; }

            ProcessTickQueue();

            // 실시간성을 위해 틱이 없더라도 현재 시간을 축의 끝점으로 갱신
            double currentTimeOA = DateTime.Now.ToOADate();
            if (currentTimeOA > _lastDataTimeOA) _lastDataTimeOA = currentTimeOA;

            lock (_dataLock)
            {
                _candlePlot.Clear();
                _volumePlot.Clear();

                LastTimestampOA = _lastDataTimeOA;

                // 렌더링 그룹(가격/거래량)에 맞춰 시리즈 순회 렌더링
                foreach (var s in _seriesList.Where(x => x.IsVisible))
                {
                    if (s.TargetGroup == AxisGroup.Price)
                        s.Render(_candlePlot, _candlePlot.Axes.Left);
                    else
                        s.Render(_volumePlot, _volumePlot.Axes.Left);
                }

                // 현재가선 별도 렌더링
                if (_currentPriceLine != null)
                    _candlePlot.Add.Plottable(_currentPriceLine);

                ApplyFixedLimits();
                _formsPlot.Refresh();
            }
        }

        private void ApplyFixedLimits()
        {
            if (!_isAutoScroll) return;

            // X축 범위: 현재 데이터 시간 + 30초 여백
            double rightLimit = _lastDataTimeOA + _rightMarginSpan;
            double leftLimit = rightLimit - (_fixedSpan + _rightMarginSpan);

            _candlePlot.Axes.Bottom.Range.Set(leftLimit, rightLimit);
            _volumePlot.Axes.Bottom.Range.Set(_candlePlot.Axes.Bottom.Range);

            // Y축 오토스케일 (가격 차트)
            if (!_isYAxisLocked)
            {
                _candlePlot.Axes.AutoScaleY();
                var yRange = _candlePlot.Axes.Left.Range;
                if (yRange.Span > 0)
                {
                    double pad = yRange.Span * 0.15; // 상하 15% 여유
                    _candlePlot.Axes.Left.Range.Set(yRange.Min - pad, yRange.Max + pad);
                }
            }

            // Y축 오토스케일 (거래량 차트)
            _volumePlot.Axes.AutoScaleY();
            _volumePlot.Axes.Left.Range.Set(0, _volumePlot.Axes.Left.Range.Max * 1.1);
        }

        private void SetupMultiplot()
        {
            _formsPlot.Multiplot.AddPlots(2);
            _candlePlot = _formsPlot.Multiplot.Subplots.GetPlot(0);
            _volumePlot = _formsPlot.Multiplot.Subplots.GetPlot(1);
            _formsPlot.Multiplot.Layout = new TwoRowLayout(0.75f); // 7.5 : 2.5 비율

            var pad = new PixelPadding(75, 120, 20, 35); // 좌, 우(레이블 공간), 상, 하
            _candlePlot.Layout.Fixed(pad);
            _volumePlot.Layout.Fixed(pad);

            ConfigurePlot(_candlePlot, "Price (KRW)", true);
            ConfigurePlot(_volumePlot, "Volume", false);
        }

        private void ConfigurePlot(Plot plot, string yLabel, bool showXLabel)
        {
            plot.Axes.DateTimeTicksBottom();
            var dtGen = new ScottPlot.TickGenerators.DateTimeAutomatic();
            dtGen.LabelFormatter = dt => dt.ToString("HH:mm:ss");
            plot.Axes.Bottom.TickGenerator = dtGen;

            plot.YLabel(yLabel);
            plot.FigureBackground.Color = Colors.Black;
            plot.DataBackground.Color = Colors.Black;
            plot.Grid.MajorLineColor = Colors.Gray.WithAlpha(0.15);
            plot.Axes.Color(Colors.Gray);

            if (!showXLabel) plot.Axes.Bottom.TickLabelStyle.IsVisible = false;
        }

        public void UpdateCurrentPrice(double price)
        {
            lock (_dataLock)
            {
                if (_currentPriceLine == null)
                {
                    _currentPriceLine = new HorizontalLine();
                    ConfigurePriceLine(_currentPriceLine);
                }

                _currentPriceLine.Y = price;
                _currentPriceLine.Text = price.ToString("N0");
                _currentPriceLine.LabelStyle.BackgroundColor = (price >= _lastPrice) ? Colors.Red : Colors.Blue;
                _lastPrice = price;
            }
        }

        private void ConfigurePriceLine(HorizontalLine hline)
        {
            hline.LabelStyle.FontName = _malgunFontName;
            hline.LabelStyle.BorderRadius = 3;
            hline.LabelStyle.PixelPadding = new PixelPadding(3, 3, 3, 2);
            hline.TextRotation = 0;
            hline.LabelOppositeAxis = true; // 우측 축에 라벨 표시
        }

        private void SetupMouseInteraction()
        {
            _formsPlot.MouseDown += (s, e) => {
                _isAutoScroll = false;
                _isYAxisLocked = true;
            };

            _formsPlot.MouseClick += (s, e) => {
                if (e.Button == MouseButtons.Right) ResetZoom();
            };

            // 가격 차트와 거래량 차트의 X축 동기화
            _candlePlot.RenderManager.AxisLimitsChanged += (s, e) =>
            {
                if (!_isAutoScroll)
                {
                    var newRange = _candlePlot.Axes.Bottom.Range;
                    _volumePlot.Axes.Bottom.Range.Set(newRange.Min, newRange.Max);
                }
            };
        }

        public void ResetZoom()
        {
            lock (_dataLock)
            {
                _isAutoScroll = true;
                _isYAxisLocked = false;
            }
        }

        /// <summary>
        /// 현재 확대 비율을 유지하면서 타임라인만 최신으로 이동시킵니다.
        /// </summary>
        public void ResetTimelineOnly()
        {
            lock (_dataLock)
            {
                var currentRange = _candlePlot.Axes.Bottom.Range;
                double currentSpan = currentRange.Span;

                _isAutoScroll = true;

                double currentTimeOA = DateTime.Now.ToOADate();
                if (currentTimeOA > _lastDataTimeOA) _lastDataTimeOA = currentTimeOA;

                double rightLimit = _lastDataTimeOA + _rightMarginSpan;
                double leftLimit = rightLimit - currentSpan;

                _candlePlot.Axes.Bottom.Range.Set(leftLimit, rightLimit);
                _volumePlot.Axes.Bottom.Range.Set(leftLimit, rightLimit);

                // 확대 비율 유지
                _fixedSpan = currentSpan - _rightMarginSpan;
            }

            if (_formsPlot.IsHandleCreated)
            {
                _formsPlot.BeginInvoke(new Action(() => _formsPlot.Refresh()));
            }
        }

        public T? GetSeries<T>(ExchangeSource source, SeriesType type) where T : class, IChartSeries
        {
            lock (_dataLock)
            {
                var series = _seriesList.FirstOrDefault(s => s.Source == source && s.Type == type);
                return series as T;
            }
        }
    }

    /// <summary>
    /// 상단(가격)과 하단(거래량)의 비율을 조절하는 멀티플롯 레이아웃입니다.
    /// </summary>
    public class TwoRowLayout : ScottPlot.IMultiplotLayout
    {
        private readonly float _topFraction;
        public TwoRowLayout(float topFraction) => _topFraction = topFraction;
        public PixelRect[] GetSubplotRectangles(SubplotCollection subplots, PixelRect figureRect)
        {
            PixelRect[] rects = new PixelRect[subplots.Count];
            float topHeight = figureRect.Height * _topFraction;
            rects[0] = new PixelRect(new PixelSize(figureRect.Width, topHeight)).WithDelta(figureRect.Left, figureRect.Top);
            if (subplots.Count > 1)
                rects[1] = new PixelRect(new PixelSize(figureRect.Width, figureRect.Height - topHeight)).WithDelta(figureRect.Left, figureRect.Top + topHeight);
            return rects;
        }
    }
}