// ✅ [수정] HeatmapForm.cs
// 🔥 컬러맵 재설계: 색약 친화 + 타이틀(Yellow/White/Green/Red) 일치
//    값 구간:
//      0.00 ~ 0.60  → 검정~파랑   (일반 물량 얇음~두꺼움)
//      0.61 ~ 0.89  → 파랑~노랑   (대량 호가 Active: Yellow)
//      0.90 ~ 0.94  → 흰색        (최대 호가 벽: White)
//      0.95 ~ 0.97  → 초록        (체결/소멸: Green = FilledAndGone)
//      0.98 ~ 1.00  → 빨강        (스푸핑 의심: Red = SpoofingSuspect)
// 🔥 캔들 오버레이 토글 유지

using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Upbit_Manager.Core;
using Upbit_Manager.Core.Orderbook;
using Upbit_Manager.UI.Series;

namespace Upbit_Manager
{
    public sealed partial class HeatmapForm : Form
    {
        #region [ 필드 ]

        private readonly OrderbookHeatmapEngine _engine;
        private readonly FormsPlot _formsPlot;

        private const int RenderIntervalMs = 500;
        private System.Windows.Forms.Timer _renderTimer = null!;

        private readonly object _resultLock = new();
        private RenderPayload? _pending = null;
        private bool _hasNew = false;

        private readonly object _calcLock = new();
        private bool _isCalculating = false;
        private bool _calcRequested = false;

        private ScottPlot.Plottables.Heatmap? _heatmapPlottable;

        private bool _userManualZoom = false;
        private volatile bool _isLoadingHistory = false;

        // ── 캔들 오버레이 ─────────────────────────────────────────
        private CandlestickPlot? _overlayCandlePlot = null;
        private HorizontalLine? _overlayPriceLine = null;
        private UpbitCandleSeries? _candleSeries = null;
        private string _heatmapMarket = "KRW-ADA";
        private bool _candleOverlayOn = false;

        private ToolStrip? _toolStrip;
        private ToolStripButton? _btnCandleToggle;

        // 🔥 색약 친화 커스텀 컬러맵 (0.0~1.0 전체 범위 커버)
        //    검정 → 파랑 → 노랑(Active) → 흰색(Wall) → 초록(Filled) → 빨강(Spoofing)
        private static readonly ScottPlot.Color[] HeatmapColors = new[]
        {
            ScottPlot.Color.FromHex("#000000"),  // 0.00  물량 없음: 검정
            ScottPlot.Color.FromHex("#0a0a2a"),  // 0.07  아주 적음
            ScottPlot.Color.FromHex("#0033AA"),  // 0.14  적은 물량: 진한 파랑
            ScottPlot.Color.FromHex("#0055FF"),  // 0.21  중간 물량: 파랑
            ScottPlot.Color.FromHex("#0099FF"),  // 0.29  중상 물량: 하늘색
            ScottPlot.Color.FromHex("#00CCFF"),  // 0.36  두꺼운 물량: 밝은 하늘
            ScottPlot.Color.FromHex("#FFCC00"),  // 0.43  대량 호가 시작: 노랑 (Active Yellow)
            ScottPlot.Color.FromHex("#FFDD44"),  // 0.50  대량 호가: 밝은 노랑
            ScottPlot.Color.FromHex("#FFEE88"),  // 0.57  대량 호가 강: 연노랑
            ScottPlot.Color.FromHex("#FFFFFF"),  // 0.64  최대 호가 벽: 흰색 (Active White)
            ScottPlot.Color.FromHex("#FFFFFF"),  // 0.71  흰색 유지
            ScottPlot.Color.FromHex("#AAFFAA"),  // 0.79  체결 시작: 연초록
            ScottPlot.Color.FromHex("#00FF66"),  // 0.86  체결/소멸: 초록 (FilledAndGone Green)
            ScottPlot.Color.FromHex("#FF4444"),  // 0.93  스푸핑 시작: 빨강 (Spoofing Red)
            ScottPlot.Color.FromHex("#FF0000"),  // 1.00  스푸핑 강: 진한 빨강
        };

        #endregion

        #region [ 생성자 ]

        public HeatmapForm(OrderbookHeatmapEngine engine)
        {
            _engine = engine;

            Text = "오더북 히트맵";
            Size = new System.Drawing.Size(1400, 700);
            MinimumSize = new System.Drawing.Size(600, 400);
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.Sizable;

            // ── 툴바 ──────────────────────────────────────────────
            _toolStrip = new ToolStrip { Dock = DockStyle.Top };

            _btnCandleToggle = new ToolStripButton("캔들 OFF")
            {
                CheckOnClick = true,
                Checked = false,
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
                ForeColor = System.Drawing.Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            _btnCandleToggle.CheckedChanged += OnCandleToggleChanged;
            _toolStrip.Items.Add(_btnCandleToggle);

            // ── FormsPlot ─────────────────────────────────────────
            _formsPlot = new FormsPlot { Dock = DockStyle.Fill };

            Controls.Add(_formsPlot);
            Controls.Add(_toolStrip);

            SetupPlot();

            _renderTimer = new System.Windows.Forms.Timer { Interval = RenderIntervalMs };
            _renderTimer.Tick += (_, _) => TryRender();
            _renderTimer.Start();

            FormClosing += (_, e) =>
            {
                e.Cancel = true;
                Hide();
            };

            _formsPlot.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    _userManualZoom = false;
                    lock (_resultLock) { if (_pending != null) _hasNew = true; }
                }
            };

            _formsPlot.MouseMove += (_, e) =>
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
                    _userManualZoom = true;
            };
            _formsPlot.MouseWheel += (_, _) => _userManualZoom = true;
        }

        #endregion

        #region [ 차트 설정 ]

        private void SetupPlot()
        {
            var plot = _formsPlot.Plot;

            // 🔥 타이틀과 컬러맵 일치
            plot.Title("Orderbook Heatmap  |  Yellow/White: Active Orders  |  Green: Filled/Expired  |  Red: Spoofing Suspected");
            plot.XLabel("Time");
            plot.YLabel("Price (KRW)");

            plot.Axes.DateTimeTicksBottom();
            var dtGen = new ScottPlot.TickGenerators.DateTimeAutomatic();
            dtGen.LabelFormatter = dt =>
                dt.Hour == 0 && dt.Minute == 0
                    ? dt.ToString("MM/dd\n00:00")
                    : dt.ToString("HH:mm");
            plot.Axes.Bottom.TickGenerator = dtGen;

            double now = DateTime.Now.ToOADate();
            plot.Axes.SetLimitsX(
                now - TimeSpan.FromHours(6).TotalDays,
                now + TimeSpan.FromMinutes(2).TotalDays);
            plot.Axes.SetLimitsY(0, 1000);

            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1e1e1e");
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#252526");
            plot.Axes.Color(ScottPlot.Color.FromHex("#d4d4d4"));
        }

        #endregion

        #region [ 캔들 오버레이 ]

        public void SetCandleOverlay(UpbitCandleSeries? series, string market)
        {
            if (_isLoadingHistory) return;
            _candleSeries = series;

            bool isHeatmapMarket = market.Equals(_heatmapMarket, StringComparison.OrdinalIgnoreCase);

            if (_btnCandleToggle != null)
            {
                _btnCandleToggle.Enabled = isHeatmapMarket;

                if (!isHeatmapMarket)
                {
                    _candleOverlayOn = false;
                    _btnCandleToggle.Checked = false;
                    _btnCandleToggle.Text = $"캔들 OFF ({market} 미지원)";
                    RemoveCandleOverlay();
                }
                else
                {
                    _btnCandleToggle.Text = _candleOverlayOn ? "캔들 ON" : "캔들 OFF";
                }
            }

            if (isHeatmapMarket && _candleOverlayOn)
                lock (_resultLock) { _hasNew = true; }
        }

        private void OnCandleToggleChanged(object? sender, EventArgs e)
        {
            if (_btnCandleToggle == null) return;

            _candleOverlayOn = _btnCandleToggle.Checked;
            _btnCandleToggle.Text = _candleOverlayOn ? "캔들 ON" : "캔들 OFF";
            _btnCandleToggle.ForeColor = _candleOverlayOn
                ? System.Drawing.Color.LightGreen
                : System.Drawing.Color.White;

            if (!_candleOverlayOn)
                RemoveCandleOverlay();

            lock (_resultLock) { _hasNew = true; }
        }

        private void RemoveCandleOverlay()
        {
            var plot = _formsPlot.Plot;

            if (_overlayCandlePlot != null)
            {
                plot.Remove(_overlayCandlePlot);
                _overlayCandlePlot = null;
            }
            if (_overlayPriceLine != null)
            {
                plot.Remove(_overlayPriceLine);
                _overlayPriceLine = null;
            }
        }

        private void RenderCandleOverlay()
        {
            if (!_candleOverlayOn || _candleSeries == null) return;

            var plot = _formsPlot.Plot;
            var snapshot = _candleSeries.GetRenderSnapshot();
            if (snapshot.Count == 0) return;

            if (_overlayCandlePlot != null)
                plot.Remove(_overlayCandlePlot);

            _overlayCandlePlot = plot.Add.Candlestick(snapshot);
            _overlayCandlePlot.RisingColor = Colors.Red.WithAlpha(0.5);
            _overlayCandlePlot.FallingColor = Colors.Blue.WithAlpha(0.5);
            _overlayCandlePlot.Sequential = false;

            double lastPrice = _candleSeries.LastPrice;
            if (lastPrice > 0)
            {
                if (_overlayPriceLine != null)
                    plot.Remove(_overlayPriceLine);

                _overlayPriceLine = plot.Add.HorizontalLine(lastPrice);
                _overlayPriceLine.LineStyle.Width = 1;
                _overlayPriceLine.LinePattern = LinePattern.Dashed;
                _overlayPriceLine.LineStyle.Color = Colors.Yellow.WithAlpha(0.8);
                _overlayPriceLine.Text = lastPrice.ToString("N0");
                _overlayPriceLine.LabelFontColor = Colors.White;
                _overlayPriceLine.LabelBackgroundColor =
                    lastPrice >= _candleSeries.PrevPrice ? Colors.Red : Colors.Blue;
                _overlayPriceLine.LabelOppositeAxis = true;
                _overlayPriceLine.TextAlignment = Alignment.MiddleLeft;
                _overlayPriceLine.LabelStyle.OffsetX = 2;
            }
        }

        #endregion

        #region [ 데이터 수신 ]

        public void PushSnapshot(OrderbookSnapshot snapshot)
        {
            if (_isLoadingHistory) return;
            _engine.ProcessSnapshot(snapshot);
            RequestBackgroundCalc();
        }

        public void PushHistorySnapshot(OrderbookSnapshot snapshot)
        {
            _isLoadingHistory = true;
            _engine.IsLoadingHistory = true;
            _engine.ProcessSnapshot(snapshot);
        }

        public void OnHistoryCompleted()
        {
            _engine.IsLoadingHistory = false;
            _isLoadingHistory = false;

            var cells = _engine.GetCells();
            Logger.Log($"[Heatmap] 히스토리 로드 완료 → 셀 수: {cells.Count}");

            if (cells.Count > 0)
            {
                double minOA = cells.Min(c => c.TimeOA);
                double maxOA = cells.Max(c => c.TimeOA);
                Logger.Log($"[Heatmap] 셀 시간 범위: {DateTime.FromOADate(minOA):HH:mm} ~ {DateTime.FromOADate(maxOA):HH:mm}");
            }

            RequestBackgroundCalc();
        }

        #endregion

        #region [ 백그라운드 계산 ]

        private void RequestBackgroundCalc()
        {
            lock (_calcLock)
            {
                if (_isCalculating)
                {
                    _calcRequested = true;
                    return;
                }
                _isCalculating = true;
                _calcRequested = false;
            }

            Task.Run(() =>
            {
                while (true)
                {
                    var result = BuildPayload();

                    lock (_resultLock)
                    {
                        _pending = result;
                        _hasNew = true;
                    }

                    lock (_calcLock)
                    {
                        if (!_calcRequested)
                        {
                            _isCalculating = false;
                            break;
                        }
                        _calcRequested = false;
                    }
                }
            });
        }

        private RenderPayload? BuildPayload()
        {
            var cells = _engine.GetCells();
            if (cells.Count == 0) return null;

            var timeKeys = cells.Select(c => c.TimeOA).Distinct().OrderBy(x => x).ToList();
            var priceKeys = cells.Select(c => c.Price).Distinct().OrderBy(x => x).ToList();

            int nTime = timeKeys.Count;
            int nPrice = priceKeys.Count;
            if (nTime == 0 || nPrice == 0) return null;

            var timeIdx = timeKeys.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i);
            var priceIdx = priceKeys.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i);

            double maxVol = cells.Max(c => c.Volume);
            if (maxVol <= 0) return null;

            var data = new double[nPrice, nTime];

            foreach (var cell in cells)
            {
                int col = timeIdx[cell.TimeOA];
                int row = priceIdx[cell.Price];

                // 🔥 모든 값을 0.0~1.0 범위 안에 배치
                //    컬러맵 구간:
                //      0.00~0.59  검정~하늘 (일반 물량)
                //      0.60~0.89  노랑~흰색 (대량 호가 Active)
                //      0.90~0.94  초록      (체결/소멸 FilledAndGone)
                //      0.95~1.00  빨강      (스푸핑 SpoofingSuspect)
                data[row, col] = cell.State switch
                {
                    HeatmapCellState.FilledAndGone => 0.92, // 🟢 초록 구간
                    HeatmapCellState.SpoofingSuspect => 0.98, // 🔴 빨강 구간
                    _ => cell.Volume / maxVol switch
                    {
                        // 일반 물량: 0.0~0.59 (검정~하늘)
                        var r when r < 0.7 => Math.Clamp(r * 0.59 / 0.7, 0.02, 0.59),
                        // 대량 물량: 0.60~0.89 (노랑~흰색)
                        var r => Math.Clamp(0.60 + (r - 0.7) / 0.3 * 0.29, 0.60, 0.89)
                    }
                };
            }

            double halfT = TimeSpan.FromSeconds(_engine.TimeBucketSeconds / 2.0).TotalDays;
            double halfP = _engine.PriceBucketSize * 0.5;

            return new RenderPayload
            {
                Data = data,
                XMin = timeKeys.First() - halfT,
                XMax = timeKeys.Last() + halfT,
                YMin = priceKeys.First() - halfP,
                YMax = priceKeys.Last() + halfP
            };
        }

        #endregion

        #region [ 렌더링 (500ms 타이머) ]

        private void TryRender()
        {
            if (_isLoadingHistory) return;
            if (!IsHandleCreated) return;

            RenderPayload? payload;
            lock (_resultLock)
            {
                if (!_hasNew) return;
                payload = _pending;
                _hasNew = false;
            }

            var plot = _formsPlot.Plot;

            if (payload == null)
            {
                if (_heatmapPlottable != null)
                {
                    plot.Remove(_heatmapPlottable);
                    _heatmapPlottable = null;
                }
                RemoveCandleOverlay();
                if (Visible) _formsPlot.Refresh();
                return;
            }

            // ── 히트맵 갱신 ────────────────────────────────────────
            if (_heatmapPlottable != null)
                plot.Remove(_heatmapPlottable);

            _heatmapPlottable = plot.Add.Heatmap(payload.Data);
            _heatmapPlottable.Position = new CoordinateRect(
                payload.XMin,
                payload.XMax,
                payload.YMin,
                payload.YMax);

            // 🔥 커스텀 컬러맵 적용 (색약 친화 + 타이틀 일치)
            _heatmapPlottable.Colormap = new ScottPlot.Colormaps.Custom(HeatmapColors);
            _heatmapPlottable.FlipVertically = false;

            // 🔥 캔들 오버레이 (히트맵 위에 렌더링)
            RenderCandleOverlay();

            // ── X/Y 축 자동 스크롤 ─────────────────────────────────
            if (!_userManualZoom)
            {
                double now = DateTime.Now.ToOADate();
                double xPadding = TimeSpan.FromMinutes(1).TotalDays;
                double xLeft = now - TimeSpan.FromHours(6).TotalDays;
                double xMin = Math.Min(payload.XMin, xLeft);
                plot.Axes.SetLimitsX(xMin, now + xPadding);
                plot.Axes.SetLimitsY(payload.YMin - 2, payload.YMax + 2);
            }

            if (Visible) _formsPlot.Refresh();
        }

        #endregion

        #region [ 외부 호출 ]

        public void ForceRefresh()
        {
            _userManualZoom = false;
            lock (_resultLock) { if (_pending != null) _hasNew = true; }
        }

        #endregion

        #region [ 내부 DTO ]

        private sealed class RenderPayload
        {
            public double[,] Data { get; init; } = new double[0, 0];
            public double XMin { get; init; }
            public double XMax { get; init; }
            public double YMin { get; init; }
            public double YMax { get; init; }
        }

        #endregion
    }
}