using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Upbit_Manager.Models.Common;
using Upbit_Manager.UI.Series;
using UpbitManager.Models.Upbit;

namespace Upbit_Manager.UI
{
    public class ChartManager
    {

        
        private double _lastPrice = 0;

        public static double LastTimestampOA { get; private set; }

        private readonly FormsPlot _formsPlot;
        private Plot _candlePlot;
        private Plot _volumePlot;

        private HorizontalLine _currentPriceLine; // 재사용할 라인 객체
        
        // 1. 폰트 이름을 담아둘 필드 (캐싱)
        private readonly string _malgunFontName;

        private readonly PixelPadding _defaultPadding = new PixelPadding(3, 3, 3, 2); // 좌, 우, 상, 하

        private readonly object _dataLock = new();
        private readonly List<IChartSeries> _seriesList = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<TradeTick> _tickQueue = new();

        private bool _isAutoScroll = true;
        private bool _isYAxisLocked = false;

        // ─── 설정 값 ──────────────────────────────────────────
        private readonly double _fixedSpan = TimeSpan.FromMinutes(200).TotalDays; // 200분 폭
        private readonly double _rightMarginSpan = TimeSpan.FromSeconds(30).TotalDays; // ⭐ 30초 여백
        private double _lastDataTimeOA = 0;

        public ChartManager(FormsPlot formsPlot)
        {
            _formsPlot = formsPlot;

            // 2. 생성자 또는 초기화 시점에 딱 한 번만 실행
            using (Font malgun = new Font("맑은 고딕", 12))
            {
                _malgunFontName = malgun.Name;
            }

            // 3. (선택 사항) ScottPlot 전역 기본 폰트로 지정해버리기
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

        public void SetSeriesVisible(ExchangeSource source, SeriesType type, bool visible)
        {
            lock (_dataLock)
            {
                var series = _seriesList.FirstOrDefault(s => s.Source == source && s.Type == type);
                if (series != null) series.IsVisible = visible;
            }
        }

        public void PushData(SeriesType type, object payload, ExchangeSource source = ExchangeSource.Upbit)
        {
            lock (_dataLock)
            {
                // 1. 넘겨받은 type(VolumeLimit)과 일치하는 시리즈만 필터링
                var targets = _seriesList.Where(s => s.Type == type && s.Source == source);
                // 2. 해당 시리즈의 UpdateData만 호출!
                foreach (var s in targets) s.UpdateData(payload);
            }
        }

        public void EnqueueTick(double price, double vol, string side)
        {
            _tickQueue.Enqueue(new TradeTick { Price = price, Volume = vol, Side = side, Time = DateTime.Now });
        }

        public void ResetZoom() { lock (_dataLock) { _isAutoScroll = true; _isYAxisLocked = false; } }

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

        // ChartManager.cs 내부
        private void ProcessTickQueue()
        {
            while (_tickQueue.TryDequeue(out var t))
            {
                _lastDataTimeOA = t.Time.ToOADate();
                // ⭐ 여기서 오직 'UpbitRealtimePayload' 객체만 생성해서 쏘고 있습니다.
                var payload = new UpbitRealtimePayload(t.Price, t.Volume, t.Side);
                lock (_dataLock) { foreach (var s in _seriesList) s.UpdateData(payload); }
            }
        }

        public void UpdateUI()
        {
            if (_formsPlot.InvokeRequired) { _formsPlot.Invoke(new Action(UpdateUI)); return; }

            ProcessTickQueue();

            // ⭐ 실시간성을 위해 틱 유무와 상관없이 현재 시간을 끝점으로 갱신
            double currentTimeOA = DateTime.Now.ToOADate();
            if (currentTimeOA > _lastDataTimeOA) _lastDataTimeOA = currentTimeOA;

            lock (_dataLock)
            {
                _candlePlot.Clear();
                _volumePlot.Clear();

                LastTimestampOA = _lastDataTimeOA;

                // ChartManager.cs의 UpdateUI 내부
                foreach (var s in _seriesList.Where(x => x.IsVisible))
                {
                    // 객체가 "나 위쪽이야" 하면 상단에, "나 아래쪽이야" 하면 하단에 렌더링
                    if (s.TargetGroup == AxisGroup.Price)
                    {
                        s.Render(_candlePlot, _candlePlot.Axes.Left);
                    }
                    else // AxisGroup.Volume 인 경우
                    {
                        s.Render(_volumePlot, _volumePlot.Axes.Left);
                    }
                }

                ApplyFixedLimits();
                _formsPlot.Refresh();
            }
        }

        private void ApplyFixedLimits()
        {
            if (!_isAutoScroll) return;

            // ⭐ 여백 계산 로직
            // 현재 시간(_lastDataTimeOA)을 기준으로 30초 더한 지점을 우측 끝으로 잡음
            double rightLimit = _lastDataTimeOA + _rightMarginSpan;
            double leftLimit = rightLimit - (_fixedSpan + _rightMarginSpan);

            // X축 범위 설정 (Price와 Volume 차트 동기화)
            _candlePlot.Axes.Bottom.Range.Set(leftLimit, rightLimit);
            _volumePlot.Axes.Bottom.Range.Set(_candlePlot.Axes.Bottom.Range);

            // Y축 오토스케일 (여백 공간 제외, 실제 데이터 영역 기준)
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
        }

        private void SetupMultiplot()
        {
            _formsPlot.Multiplot.AddPlots(2);
            _candlePlot = _formsPlot.Multiplot.Subplots.GetPlot(0);
            _volumePlot = _formsPlot.Multiplot.Subplots.GetPlot(1);
            _formsPlot.Multiplot.Layout = new TwoRowLayout(0.75f);

            // ⭐ 수정: PixelPadding(왼쪽, 오른쪽, 위, 아래)
            // 두 번째 값(오른쪽)을 60에서 100~120 정도로 늘립니다.
            var pad = new PixelPadding(75, 120, 20, 35);

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



        // 2. 외부(MainController)에서 호출하는 유일한 창구
        public void UpdateCurrentPrice(double price)
        {
            lock (_dataLock)
            {
                if (_currentPriceLine == null)
                {
                    // 여기서 객체를 생성하고 '맑은 고딕' 설정을 입힙니다.
                    _currentPriceLine = new ScottPlot.Plottables.HorizontalLine();
                    ConfigurePriceLine(_currentPriceLine);
                }

                _currentPriceLine.Y = price;
                _currentPriceLine.Text = price.ToString("N0");

                // 색상 로직 (상승/하락)
                _currentPriceLine.LabelStyle.BackgroundColor = (price >= _lastPrice) ? Colors.Red : Colors.Blue;
                _lastPrice = price;
            }
        }


        public void ConfigurePriceLine(HorizontalLine hline)
        {
            hline.LabelStyle.FontName = _malgunFontName;
            hline.LabelStyle.BorderRadius = 3;
            hline.LabelStyle.PixelPadding = new PixelPadding(3, 3, 3, 2);
            //hline.TextAlignment = Alignment.MiddleLeft;
            //hline.LabelOppositeAxis = true;
            hline.TextRotation = 0;
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

        public void ResetTimelineOnly()
        {
            lock (_dataLock)
            {
                // 1. 현재 사용자가 보고 있는 X축의 폭(Span)을 계산합니다.
                // 이 과정을 거쳐야 확대/축소된 비율이 유지됩니다.
                var currentRange = _candlePlot.Axes.Bottom.Range;
                double currentSpan = currentRange.Span;

                // 2. 자동 스크롤을 다시 켭니다.
                _isAutoScroll = true;

                // 3. 현재 시간 정보를 갱신합니다.
                double currentTimeOA = DateTime.Now.ToOADate();
                if (currentTimeOA > _lastDataTimeOA) _lastDataTimeOA = currentTimeOA;

                // 4. [중요] 사용자가 보고 있던 폭(currentSpan)을 기반으로 새로운 범위를 잡습니다.
                // _fixedSpan을 사용하지 않고 currentSpan을 사용함으로써 확대 비율을 유지합니다.
                double rightLimit = _lastDataTimeOA + _rightMarginSpan;
                double leftLimit = rightLimit - currentSpan;

                // 5. 계산된 범위를 적용합니다.
                _candlePlot.Axes.Bottom.Range.Set(leftLimit, rightLimit);
                _volumePlot.Axes.Bottom.Range.Set(leftLimit, rightLimit);

                // 6. (선택사항) 만약 이후로도 이 확대 비율을 계속 유지하며 흐르게 하고 싶다면
                // 내부 필드인 _fixedSpan을 현재 폭으로 업데이트합니다.
                // fieldInfo 등을 사용하지 않고 직접 접근 가능한 필드라면 아래 주석을 해제하세요.
                // _fixedSpan = currentSpan - _rightMarginSpan; 
            }

            // UI 즉시 새로고침
            if (_formsPlot.IsHandleCreated)
            {
                _formsPlot.BeginInvoke(new Action(() => _formsPlot.Refresh()));
            }
        }

        #region [ 추가된 연결 메서드 ]

        /// <summary>
        /// 특정 거래소와 시리즈 타입에 해당하는 객체를 찾아 반환합니다.
        /// 알람 엔진이나 외부 컨트롤러에서 특정 데이터 시리즈에 접근할 때 사용합니다.
        /// </summary>
        /// <typeparam name="T">반환받고자 하는 시리즈의 클래스 타입 (예: UpbitCandleSeries)</typeparam>
        /// <param name="source">거래소 구분 (Upbit, Binance 등)</param>
        /// <param name="type">시리즈 구분 (Candle, Volume, PriceLine 등)</param>
        /// <returns>일치하는 시리즈 객체 (없을 경우 null)</returns>

        public T? GetSeries<T>(ExchangeSource source, SeriesType type) where T : class, IChartSeries
        {
            lock (_dataLock) // ChartManager 내부에 정의된 lock 객체 사용
            {
                // ChartManager 내부에 있는 _seriesList를 참조합니다.
                var series = _seriesList.FirstOrDefault(s => s.Source == source && s.Type == type);
                return series as T;
            }
        }
        #endregion
    }

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