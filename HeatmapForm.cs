// ✅ [수정] HeatmapForm.cs
// 🔥 자동 스크롤 ON 시 Y축 위치(중심) + 크기(span) 모두 유지
//    → 수동으로 확대/이동한 상태 그대로, X축 우측만 현재시각에 붙음
//    → yMid를 payload 기준이 아닌 현재 축 limits에서 읽어옴

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

        // 레이어 1: 일반 물량 히트맵
        private ScottPlot.Plottables.Heatmap? _volumeHeatmap = null;
        // 레이어 2: FilledAndGone(초록) + Spoofing(빨강) 오버레이
        private ScottPlot.Plottables.Heatmap? _stateOverlay = null;

        // 🔥 자동 스크롤 ON/OFF
        private bool _autoScroll = true;

        // 🔥 사용자가 수동 조작 후 저장된 축 상태
        //    - _savedYMin / _savedYMax : 마지막으로 저장된 Y축 범위
        //    - _savedXSpan             : 마지막으로 저장된 X축 폭(시간 범위 크기)
        //    - 0 이면 아직 저장 안 됨 → 자동 스크롤 시 기본값 사용
        private double _savedYMin = 0;
        private double _savedYMax = 0;
        private double _savedXSpan = 0; // 🔥 X축 시간 폭 (OADate 단위)

        private volatile bool _isLoadingHistory = false;

        // ── 캔들 오버레이 ─────────────────────────────────────────
        private CandlestickPlot? _overlayCandlePlot = null;
        private HorizontalLine? _overlayPriceLine = null;
        private UpbitCandleSeries? _candleSeries = null;
        private string _heatmapMarket = "KRW-ADA";
        private bool _candleOverlayOn = false;

        // 툴바 버튼
        private ToolStrip? _toolStrip;
        private ToolStripButton? _btnCandleToggle;
        private ToolStripButton? _btnAutoScroll;   // 자동 스크롤 버튼
        private ToolStripButton? _btnManualFix;    // 수동 고정 버튼

        // ── 컬러맵 정의 ───────────────────────────────────────────

        // 레이어 1 컬러맵: 물량 전용 (검정→파랑→노랑→흰색)
        // 초록/빨강 없음 → 물량이 아무리 많아도 흰색이 최대
        private static readonly ScottPlot.Color[] VolumeColors = new[]
        {
            ScottPlot.Color.FromHex("#000000"),  // 0/8  물량 없음: 검정
            ScottPlot.Color.FromHex("#001166"),  // 1/8  아주 적음: 짙은 남색
            ScottPlot.Color.FromHex("#0033AA"),  // 2/8  적음: 진한 파랑
            ScottPlot.Color.FromHex("#0066FF"),  // 3/8  중간: 파랑
            ScottPlot.Color.FromHex("#00AAFF"),  // 4/8  중상: 하늘색
            ScottPlot.Color.FromHex("#FFCC00"),  // 5/8  대량 시작: 노랑 (Active Yellow)
            ScottPlot.Color.FromHex("#FFEE66"),  // 6/8  대량: 밝은 노랑
            ScottPlot.Color.FromHex("#FFFFFF"),  // 7/8  최대 벽: 흰색 (Active White)
            ScottPlot.Color.FromHex("#FFFFFF"),  // 8/8  흰색 유지
        };

        // 레이어 2 컬러맵: 특수 상태 전용
        // 0.0 = 투명 → 레이어 1이 그대로 보임
        // 0.45 = 초록 (FilledAndGone)
        // 0.90 = 빨강 (Spoofing)
        private static readonly ScottPlot.Color[] StateColors = new[]
        {
            ScottPlot.Color.FromHex("#000000").WithAlpha(0),   // 0.0  투명 (일반 셀)
            ScottPlot.Color.FromHex("#000000").WithAlpha(0),   // 0.2  투명 유지
            ScottPlot.Color.FromHex("#00FF66").WithAlpha(200), // 0.4  체결/소멸: 초록 (FilledAndGone)
            ScottPlot.Color.FromHex("#00DD44").WithAlpha(220), // 0.6  초록 유지
            ScottPlot.Color.FromHex("#FF2222").WithAlpha(220), // 0.8  스푸핑: 빨강 (Spoofing)
            ScottPlot.Color.FromHex("#FF0000").WithAlpha(255), // 1.0  스푸핑 강: 진한 빨강
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

            // 캔들 토글 버튼
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

            _toolStrip.Items.Add(new ToolStripSeparator());

            // 🔥 자동 스크롤 버튼 (기본 활성, 초록 강조)
            _btnAutoScroll = new ToolStripButton("▶ 자동 스크롤")
            {
                BackColor = System.Drawing.Color.FromArgb(0, 100, 0),  // 진한 초록 배경
                ForeColor = System.Drawing.Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
            };
            _btnAutoScroll.Click += (_, _) => OnAutoScrollClicked();
            _toolStrip.Items.Add(_btnAutoScroll);

            // 🔥 수동 고정 버튼 (기본 비활성, 흐린 색)
            _btnManualFix = new ToolStripButton("⏸ 수동 고정")
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
                ForeColor = System.Drawing.Color.Gray,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular)
            };
            _btnManualFix.Click += (_, _) => OnManualFixClicked();
            _toolStrip.Items.Add(_btnManualFix);

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

            // 우클릭 → 자동 스크롤 ON 복귀 (Y축 저장값 유지)
            _formsPlot.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    SetAutoScroll(true);
                    lock (_resultLock) { if (_pending != null) _hasNew = true; }
                }
            };

            // 🔥 마우스 드래그/휠 → 현재 Y축 범위 저장 후 자동 스크롤 OFF
            _formsPlot.MouseMove += (_, e) =>
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
                {
                    SaveCurrentAxisLimits();
                    SetAutoScroll(false);
                }
            };
            _formsPlot.MouseWheel += (_, _) =>
            {
                SaveCurrentAxisLimits();
                SetAutoScroll(false);
            };
        }

        #endregion

        #region [ 자동 스크롤 제어 ]

        /// <summary>
        /// 🔥 현재 X/Y축 범위를 모두 저장.
        ///    Y축: min/max 그대로 보존 (위치 + 크기)
        ///    X축: span(폭)만 보존 → 자동 스크롤 시 이 폭 유지하며 우측을 현재시각에 붙임
        /// </summary>
        private void SaveCurrentAxisLimits()
        {
            var limits = _formsPlot.Plot.Axes.GetLimits();

            if (limits.Top > limits.Bottom)
            {
                _savedYMin = limits.Bottom;
                _savedYMax = limits.Top;
            }

            double xSpan = limits.Right - limits.Left;
            if (xSpan > 0)
                _savedXSpan = xSpan;
        }

        /// <summary>
        /// 🔥 [자동 스크롤] 버튼 클릭
        ///    X축 우측을 현재 시각에 붙이며 저장된 X/Y span 유지
        /// </summary>
        private void OnAutoScrollClicked()
        {
            SetAutoScroll(true);
            lock (_resultLock) { if (_pending != null) _hasNew = true; }
        }

        /// <summary>
        /// 🔥 [수동 고정] 버튼 클릭
        ///    현재 뷰를 그대로 저장하고 자동 스크롤 중단
        /// </summary>
        private void OnManualFixClicked()
        {
            SaveCurrentAxisLimits();
            SetAutoScroll(false);
        }

        /// <summary>
        /// 자동 스크롤 상태 변경 + 두 버튼 UI 동기화
        /// ON  → [자동 스크롤] 강조(초록),  [수동 고정] 흐림
        /// OFF → [자동 스크롤] 흐림,         [수동 고정] 강조(주황)
        /// </summary>
        private void SetAutoScroll(bool on)
        {
            _autoScroll = on;

            if (_btnAutoScroll != null)
            {
                _btnAutoScroll.BackColor = on
                    ? System.Drawing.Color.FromArgb(0, 100, 0)       // 활성: 진한 초록
                    : System.Drawing.Color.FromArgb(45, 45, 48);     // 비활성: 기본 배경
                _btnAutoScroll.ForeColor = on
                    ? System.Drawing.Color.White
                    : System.Drawing.Color.Gray;
                _btnAutoScroll.Font = new System.Drawing.Font(
                    "Segoe UI", 9f,
                    on ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular);
            }

            if (_btnManualFix != null)
            {
                _btnManualFix.BackColor = on
                    ? System.Drawing.Color.FromArgb(45, 45, 48)      // 비활성: 기본 배경
                    : System.Drawing.Color.FromArgb(140, 70, 0);     // 활성: 진한 주황
                _btnManualFix.ForeColor = on
                    ? System.Drawing.Color.Gray
                    : System.Drawing.Color.White;
                _btnManualFix.Font = new System.Drawing.Font(
                    "Segoe UI", 9f,
                    on ? System.Drawing.FontStyle.Regular : System.Drawing.FontStyle.Bold);
            }
        }

        #endregion

        #region [ 차트 설정 ]

        private void SetupPlot()
        {
            var plot = _formsPlot.Plot;

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

            double maxVol = cells.Where(c => c.State == HeatmapCellState.Active)
                                 .Select(c => c.Volume)
                                 .DefaultIfEmpty(1.0)
                                 .Max();

            var volumeData = new double[nPrice, nTime];
            var stateData = new double[nPrice, nTime];

            foreach (var cell in cells)
            {
                int col = timeIdx[cell.TimeOA];
                int row = priceIdx[cell.Price];

                switch (cell.State)
                {
                    case HeatmapCellState.Active:
                        volumeData[row, col] = Math.Clamp(cell.Volume / maxVol, 0.05, 1.0);
                        stateData[row, col] = 0.0;
                        break;

                    case HeatmapCellState.FilledAndGone:
                        volumeData[row, col] = 0.0;
                        stateData[row, col] = 0.45;
                        break;

                    case HeatmapCellState.SpoofingSuspect:
                        volumeData[row, col] = 0.0;
                        stateData[row, col] = 0.90;
                        break;
                }
            }

            double halfT = TimeSpan.FromSeconds(_engine.TimeBucketSeconds / 2.0).TotalDays;
            double halfP = _engine.PriceBucketSize * 0.5;

            return new RenderPayload
            {
                VolumeData = volumeData,
                StateData = stateData,
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
                RemoveHeatmapLayers();
                RemoveCandleOverlay();
                if (Visible) _formsPlot.Refresh();
                return;
            }

            // ── 레이어 1: 물량 히트맵 ──────────────────────────────
            if (_volumeHeatmap != null)
                plot.Remove(_volumeHeatmap);

            _volumeHeatmap = plot.Add.Heatmap(payload.VolumeData);
            _volumeHeatmap.Position = new CoordinateRect(
                payload.XMin, payload.XMax,
                payload.YMin, payload.YMax);
            _volumeHeatmap.Colormap = new ScottPlot.Colormaps.Custom(VolumeColors);
            // row[0]이 최하단 가격 → Y축 방향 논리 일치
            _volumeHeatmap.FlipVertically = true;

            // ── 레이어 2: 특수 상태 오버레이 ──────────────────────
            if (_stateOverlay != null)
                plot.Remove(_stateOverlay);

            _stateOverlay = plot.Add.Heatmap(payload.StateData);
            _stateOverlay.Position = new CoordinateRect(
                payload.XMin, payload.XMax,
                payload.YMin, payload.YMax);
            _stateOverlay.Colormap = new ScottPlot.Colormaps.Custom(StateColors);
            _stateOverlay.FlipVertically = true;

            // ── 캔들 오버레이 (최상단) ─────────────────────────────
            RenderCandleOverlay();

            // ── X/Y 축 자동 스크롤 ─────────────────────────────────
            if (_autoScroll)
            {
                double now = DateTime.Now.ToOADate();
                double xPadding = TimeSpan.FromSeconds(30).TotalDays; // 우측 1틱 여유

                // 🔥 X축: 저장된 span이 있으면 그 폭 유지하며 우측을 현재시각에 붙임
                //         없으면 기본 6시간 범위 표시
                if (_savedXSpan > 0)
                {
                    // 사용자가 확대한 시간 폭 그대로, 우측 끝만 현재시각으로 이동
                    plot.Axes.SetLimitsX(now + xPadding - _savedXSpan, now + xPadding);
                }
                else
                {
                    // 저장값 없음 → 기본 6시간 범위
                    double xLeft = now - TimeSpan.FromHours(6).TotalDays;
                    double xMin = Math.Min(payload.XMin, xLeft);
                    plot.Axes.SetLimitsX(xMin, now + xPadding);
                }

                // 🔥 Y축: 저장된 위치+크기 그대로 복원, 없으면 전체 범위
                if (_savedYMin != 0 || _savedYMax != 0)
                {
                    plot.Axes.SetLimitsY(_savedYMin, _savedYMax);
                }
                else
                {
                    plot.Axes.SetLimitsY(payload.YMin - 2, payload.YMax + 2);
                }
            }

            if (Visible) _formsPlot.Refresh();
        }

        private void RemoveHeatmapLayers()
        {
            var plot = _formsPlot.Plot;

            if (_volumeHeatmap != null)
            {
                plot.Remove(_volumeHeatmap);
                _volumeHeatmap = null;
            }
            if (_stateOverlay != null)
            {
                plot.Remove(_stateOverlay);
                _stateOverlay = null;
            }
        }

        #endregion

        #region [ 외부 호출 ]

        public void ForceRefresh()
        {
            SetAutoScroll(true);
            lock (_resultLock) { if (_pending != null) _hasNew = true; }
        }

        #endregion

        #region [ 내부 DTO ]

        private sealed class RenderPayload
        {
            // 레이어 1: 일반 물량 (0.0~1.0, 검정~흰색)
            public double[,] VolumeData { get; init; } = new double[0, 0];
            // 레이어 2: 특수 상태 (0.0=투명, 0.45=초록, 0.90=빨강)
            public double[,] StateData { get; init; } = new double[0, 0];
            public double XMin { get; init; }
            public double XMax { get; init; }
            public double YMin { get; init; }
            public double YMax { get; init; }
        }

        #endregion
    }
}