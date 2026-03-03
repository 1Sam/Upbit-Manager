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
    /// 5분할 레이아웃: 좌측 2단(가격/거래량), 우측 3단(호가창 및 기타 데이터)
    /// </summary>
    public class FivePanelLayout : ScottPlot.IMultiplotLayout
    {
        public PixelRect[] GetSubplotRectangles(SubplotCollection subplots, PixelRect figureRect)
        {
            PixelRect[] rects = new PixelRect[5];
            float leftWidth = figureRect.Width * 0.75f;
            float rightWidth = figureRect.Width * 0.25f;
            float leftTopHeight = figureRect.Height * 0.75f;

            // 좌측 영역 (Main Chart & Volume)
            rects[0] = new PixelRect(leftWidth, leftTopHeight).WithDelta(figureRect.Left, figureRect.Top);
            rects[1] = new PixelRect(leftWidth, figureRect.Height - leftTopHeight).WithDelta(figureRect.Left, figureRect.Top + leftTopHeight);

            // 우측 영역 (3단 분할)
            float rightPanelHeight = figureRect.Height / 3.0f;
            rects[2] = new PixelRect(rightWidth, rightPanelHeight).WithDelta(figureRect.Left + leftWidth, figureRect.Top);
            rects[3] = new PixelRect(rightWidth, rightPanelHeight).WithDelta(figureRect.Left + leftWidth, figureRect.Top + rightPanelHeight);
            rects[4] = new PixelRect(rightWidth, figureRect.Height - (rightPanelHeight * 2)).WithDelta(figureRect.Left + leftWidth, figureRect.Top + (rightPanelHeight * 2));

            return rects;
        }
    }

    public class ChartManager
    {
        private double _lastPrice = 0;
        public static double LastTimestampOA { get; private set; }

        private readonly FormsPlot _formsPlot;

        // 5개의 플롯 참조 보관
        private Plot _candlePlot;
        private Plot _volumePlot;
        private Plot _orderbookPlot;
        private Plot _extraPlot1;
        private Plot _extraPlot2;

        private HorizontalLine _currentPriceLine;
        private readonly string _malgunFontName;
        private readonly object _dataLock = new();
        private readonly List<IChartSeries> _seriesList = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<TradeTick> _tickQueue = new();

        private bool _isAutoScroll = true;
        private bool _isYAxisLocked = false;

        private double _fixedSpan = TimeSpan.FromMinutes(200).TotalDays;
        private readonly double _rightMarginSpan = TimeSpan.FromSeconds(30).TotalDays;
        private double _lastDataTimeOA = 0;

        public ChartManager(FormsPlot formsPlot)
        {
            _formsPlot = formsPlot;

            using (Font malgun = new Font("맑은 고딕", 12))
            {
                _malgunFontName = malgun.Name;
            }
            ScottPlot.Fonts.Default = _malgunFontName;

            InitializeSeries();
            SetupMultiplot();
            SetupMouseInteraction();

            var uiTimer = new System.Windows.Forms.Timer { Interval = 100 };
            uiTimer.Tick += (s, e) => { if (_formsPlot.IsHandleCreated) UpdateUI(); };
            uiTimer.Start();
        }

        private void InitializeSeries()
        {
            _seriesList.Clear();
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

        public void EnqueueTick(double price, double vol, string side)
        {
            _tickQueue.Enqueue(new TradeTick { Price = price, Volume = vol, Side = side, Time = DateTime.Now });
        }

        public void PushData(SeriesType type, object payload, ExchangeSource source = ExchangeSource.Upbit)
        {
            lock (_dataLock)
            {
                var targets = _seriesList.Where(s => s.Type == type && s.Source == source);
                foreach (var s in targets) s.UpdateData(payload);
            }
        }

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

            double currentTimeOA = DateTime.Now.ToOADate();
            if (currentTimeOA > _lastDataTimeOA) _lastDataTimeOA = currentTimeOA;

            lock (_dataLock)
            {
                // [해결] SubplotCollection에 foreach를 사용할 수 없는 문제를 직접 참조로 해결
                _candlePlot.Clear();
                _volumePlot.Clear();
                _orderbookPlot.Clear();
                _extraPlot1.Clear();
                _extraPlot2.Clear();

                LastTimestampOA = _lastDataTimeOA;

                foreach (var s in _seriesList.Where(x => x.IsVisible))
                {
                    // 시리즈 클래스 타입에 따른 렌더링 영역 분기
                    if (s.Type == SeriesType.Orderbook)
                        s.Render(_orderbookPlot, _orderbookPlot.Axes.Left);
                    else if (s.TargetGroup == AxisGroup.Price)
                        s.Render(_candlePlot, _candlePlot.Axes.Left);
                    else
                        s.Render(_volumePlot, _volumePlot.Axes.Left);
                }

                if (_currentPriceLine != null)
                    _candlePlot.Add.Plottable(_currentPriceLine);

                ApplyFixedLimits();
                _formsPlot.Refresh();
            }
        }

        private void ApplyFixedLimits()
        {
            if (!_isAutoScroll) return;

            double rightLimit = _lastDataTimeOA + _rightMarginSpan;
            double leftLimit = rightLimit - (_fixedSpan + _rightMarginSpan);

            _candlePlot.Axes.Bottom.Range.Set(leftLimit, rightLimit);
            _volumePlot.Axes.Bottom.Range.Set(_candlePlot.Axes.Bottom.Range);

            if (!_isYAxisLocked)
            {
                _candlePlot.Axes.AutoScaleY();
                var yRange = _candlePlot.Axes.Left.Range;
                if (yRange.Span > 0)
                {
                    double pad = yRange.Span * 0.15;
                    _candlePlot.Axes.Left.Range.Set(yRange.Min - pad, yRange.Max + pad);
                }
            }

            _volumePlot.Axes.AutoScaleY();
            _volumePlot.Axes.Left.Range.Set(0, _volumePlot.Axes.Left.Range.Max * 1.1);

            _orderbookPlot.Axes.AutoScale();
        }

        private void SetupMultiplot()
        {
            _formsPlot.Multiplot.AddPlots(5);

            // 인덱스를 사용하여 안전하게 할당
            _candlePlot = _formsPlot.Multiplot.Subplots.GetPlot(0);
            _volumePlot = _formsPlot.Multiplot.Subplots.GetPlot(1);
            _orderbookPlot = _formsPlot.Multiplot.Subplots.GetPlot(2);
            _extraPlot1 = _formsPlot.Multiplot.Subplots.GetPlot(3);
            _extraPlot2 = _formsPlot.Multiplot.Subplots.GetPlot(4);

            _formsPlot.Multiplot.Layout = new FivePanelLayout();

            // 좌측 패딩
            var mainPad = new PixelPadding(75, 70, 20, 35);
            _candlePlot.Layout.Fixed(mainPad);
            _volumePlot.Layout.Fixed(mainPad);

            // 우측 패딩
            var sidePad = new PixelPadding(40, 10, 10, 20);
            _orderbookPlot.Layout.Fixed(sidePad);
            _extraPlot1.Layout.Fixed(sidePad);
            _extraPlot2.Layout.Fixed(sidePad);

            ConfigurePlot(_candlePlot, "Price", true);
            ConfigurePlot(_volumePlot, "Vol", false);
            ConfigureSidePlot(_orderbookPlot, "Orderbook");
            ConfigureSidePlot(_extraPlot1, "Extra 1");
            ConfigureSidePlot(_extraPlot2, "Extra 2");
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

        private void ConfigureSidePlot(Plot plot, string title)
        {
            plot.FigureBackground.Color = Colors.Black;
            plot.DataBackground.Color = Colors.Black;
            plot.Axes.Color(Colors.Gray);
            plot.Grid.IsVisible = false;
            plot.Axes.Bottom.TickLabelStyle.IsVisible = false;
            plot.Title(title, size: 10);
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
            hline.LabelOppositeAxis = true;
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
}