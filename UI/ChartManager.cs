using Crypto.Collector.Shared;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;
using Upbit_Manager.UI.Series;

namespace Upbit_Manager.UI
{
    /// <summary>
    /// 차트 렌더링 중앙 매니저
    /// - 모든 Series를 수집하여 적절한 패널에 렌더링
    /// - 실시간 틱 처리
    /// - 오토 스크롤 / Y축 제어
    /// </summary>
    public sealed class ChartManager
    {
        #region Fields

        private readonly FormsPlot _formsPlot;

        private Plot _pricePlot = null!;
        private Plot _volumePlot = null!;
        private Plot _orderbookPlot = null!;
        private Plot _extraPlot1 = null!;
        private Plot _extraPlot2 = null!;

        private readonly List<IChartSeries> _seriesList = new();
        private readonly object _dataLock = new();

        private readonly Queue<(double Price, double Volume, string Side, DateTime Time)> _tickQueue = new();

        private double _lastTimestampOA;
        private double _lastPrice;

        private bool _isAutoScroll = true;
        private bool _isYAxisLocked;

        private readonly double _rightMarginSpan = TimeSpan.FromSeconds(30).TotalDays;
        private double _fixedSpan = TimeSpan.FromMinutes(200).TotalDays;

        private HorizontalLine? _currentPriceLine;

        #endregion

        #region Constructor

        public ChartManager(FormsPlot formsPlot)
        {
            _formsPlot = formsPlot;

            InitializeSeries();
            SetupMultiplot();
            SetupMouseInteraction();

            var timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += (_, _) =>
            {
                if (_formsPlot.IsHandleCreated)
                    UpdateUI();
            };
            timer.Start();
        }

        #endregion

        #region Series Initialization

        /// <summary>
        /// Assembly에서 IChartSeries 구현체 자동 로딩
        /// </summary>
        private void InitializeSeries()
        {
            var types = typeof(IChartSeries).Assembly.GetTypes()
                .Where(t => typeof(IChartSeries).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && !t.IsInterface);

            foreach (var type in types)
            {
                if (Activator.CreateInstance(type) is IChartSeries series)
                {
                    series.IsVisible = series.DefaultOn;
                    _seriesList.Add(series);
                }
            }
        }

        public T? GetSeries<T>(ExchangeSource source, SeriesType type)
            where T : class, IChartSeries
        {
            lock (_dataLock)
            {
                return _seriesList
                    .FirstOrDefault(s => s.Source == source && s.Type == type) as T;
            }
        }

        #endregion

        #region Public Data API

        /// <summary>
        /// 일반 데이터 전달
        /// </summary>
        public void PushData(SeriesType type, object payload, ExchangeSource source)
        {
            lock (_dataLock)
            {
                foreach (var s in _seriesList
                    .Where(x => x.Type == type && x.Source == source))
                {
                    s.UpdateData(payload);
                }
            }
        }

        /// <summary>
        /// 오더북 전용 전달 (MMF Bridge용)
        /// </summary>
        public void PushOrderbook(long timestamp, OrderbookUnit[] units)
        {
            var payload = new UpbitOrderbookPayload
            {
                Timestamp = timestamp,
                Units = units,
                TotalAskSize = units.Sum(u => u.AskSize),
                TotalBidSize = units.Sum(u => u.BidSize)
            };

            lock (_dataLock)
            {
                foreach (var s in _seriesList
                    .Where(x => x.Type == SeriesType.Orderbook))
                {
                    s.UpdateData(payload);
                }
            }
        }

        /// <summary>
        /// 틱 큐 적재
        /// </summary>
        public void EnqueueTick(double price, double volume, string side)
        {
            lock (_tickQueue)
            {
                _tickQueue.Enqueue((price, volume, side, DateTime.Now));
            }
        }

        /// <summary>
        /// 초기 데이터 세팅
        /// </summary>
        public void InitializeWithData(
            string market,
            List<CommonCandle> candles,
            List<(DateTime Time, double Price)>? binanceHistory = null,
            List<(DateTime Time, double Price)>? usdtHistory = null)
        {
            lock (_dataLock)
            {
                foreach (var s in _seriesList)
                    s.Clear();

                foreach (var s in _seriesList)
                {
                    if (s.Type == SeriesType.Candle && s.Source == ExchangeSource.Upbit)
                        s.UpdateData(candles);

                    // 추가
                    if (s.Type == SeriesType.Volume && s.Source == ExchangeSource.Upbit)
                        s.UpdateData(candles);

                    if (s.Type == SeriesType.PriceLine && s.Source == ExchangeSource.Binance && binanceHistory != null)
                        s.UpdateData(binanceHistory);

                    if (s.Type == SeriesType.PriceLine && s.Source == ExchangeSource.Upbit && usdtHistory != null)
                        s.UpdateData(usdtHistory);
                }

                _lastTimestampOA = candles.LastOrDefault()?.Time.ToOADate()
                                   ?? DateTime.Now.ToOADate();
            }

            UpdateUI();
        }

        #endregion

        #region UI Update Loop

        private void ProcessTickQueue()
        {
            lock (_tickQueue)
            {
                while (_tickQueue.Count > 0)
                {
                    var t = _tickQueue.Dequeue();
                    _lastTimestampOA = t.Time.ToOADate();

                    foreach (var s in _seriesList)
                        s.UpdateData(t);
                }
            }
        }

        public void UpdateUI()
        {
            if (_formsPlot.InvokeRequired)
            {
                _formsPlot.Invoke(UpdateUI);
                return;
            }

            ProcessTickQueue();

            lock (_dataLock)
            {
                _pricePlot.Clear();
                _volumePlot.Clear();
                _orderbookPlot.Clear();
                _extraPlot1.Clear();
                _extraPlot2.Clear();

                foreach (var s in _seriesList.Where(x => x.IsVisible))
                {
                    if (s.Type == SeriesType.Orderbook)
                        s.Render(_orderbookPlot, _orderbookPlot.Axes.Left);
                    else if (s.TargetGroup == AxisGroup.Price)
                        s.Render(_pricePlot, _pricePlot.Axes.Left);
                    else
                        s.Render(_volumePlot, _volumePlot.Axes.Left);
                }

                if (_currentPriceLine != null)
                    _pricePlot.Add.Plottable(_currentPriceLine);

                ApplyAutoScroll();

                _formsPlot.Refresh();
            }
        }

        #endregion

        #region Layout

        private void SetupMultiplot()
        {
            _formsPlot.Multiplot.AddPlots(5);

            _pricePlot = _formsPlot.Multiplot.Subplots.GetPlot(0);
            _volumePlot = _formsPlot.Multiplot.Subplots.GetPlot(1);
            _orderbookPlot = _formsPlot.Multiplot.Subplots.GetPlot(2);
            _extraPlot1 = _formsPlot.Multiplot.Subplots.GetPlot(3);
            _extraPlot2 = _formsPlot.Multiplot.Subplots.GetPlot(4);

            _formsPlot.Multiplot.Layout = new FivePanelLayout();

            // 우측 Y축 레이블 공간 고정 (자동 계산으로 인한 레이아웃 밀림 방지)
            _pricePlot.Layout.Fixed(new PixelPadding(left: 50, right: 80, bottom: 20, top: 10));
            _volumePlot.Layout.Fixed(new PixelPadding(left: 50, right: 80, bottom: 20, top: 10));

            ConfigurePlot(_pricePlot, "Price", true);
            ConfigurePlot(_volumePlot, "Volume", false);
            ConfigureSidePlot(_orderbookPlot, "Orderbook");
            ConfigureSidePlot(_extraPlot1, "Extra1");
            ConfigureSidePlot(_extraPlot2, "Extra2");
        }

        private static void ConfigurePlot(Plot plot, string yLabel, bool showX)
        {
            plot.Axes.DateTimeTicksBottom();
            plot.YLabel(yLabel);

            if (!showX)
                plot.Axes.Bottom.TickLabelStyle.IsVisible = false;
        }

        private static void ConfigureSidePlot(Plot plot, string title)
        {
            plot.Title(title);
            plot.Axes.Bottom.TickLabelStyle.IsVisible = false;
        }

        #endregion

        #region Auto Scroll

        private void ApplyAutoScroll()
        {
            if (!_isAutoScroll)
                return;

            double right = _lastTimestampOA + _rightMarginSpan;
            double left = right - (_fixedSpan + _rightMarginSpan);

            _pricePlot.Axes.SetLimitsX(left, right);
            _volumePlot.Axes.SetLimitsX(left, right);

            if (!_isYAxisLocked)
                _pricePlot.Axes.AutoScaleY();

            _volumePlot.Axes.AutoScaleY();
            _orderbookPlot.Axes.AutoScale();
        }

        #endregion

        #region Mouse Interaction

        private void SetupMouseInteraction()
        {
            _formsPlot.MouseDown += (_, _) =>
            {
                _isAutoScroll = false;
                _isYAxisLocked = true;
            };

            _formsPlot.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Right)
                    ResetZoom();
            };
        }

        public void ResetZoom()
        {
            _isAutoScroll = true;
            _isYAxisLocked = false;
        }

        public void ResetTimelineOnly()
        {
            lock (_dataLock)
            {
                _isAutoScroll = true;

                double right = _lastTimestampOA + _rightMarginSpan;
                double left = right - _fixedSpan;

                _pricePlot.Axes.SetLimitsX(left, right);
                _volumePlot.Axes.SetLimitsX(left, right);
            }

            if (_formsPlot.IsHandleCreated)
                _formsPlot.BeginInvoke(new Action(() => _formsPlot.Refresh()));
        }

        public IReadOnlyList<IChartSeries> SeriesList
        {
            get
            {
                lock (_dataLock)
                {
                    return _seriesList.ToList();
                }
            }
        }
        #endregion
    }
}