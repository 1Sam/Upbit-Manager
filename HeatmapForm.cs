// ✅ [신규] UI/HeatmapForm.cs
// 🔥 오더북 히트맵 전용 독립 폼
//    - 메인 폼과 UI 스레드 공유하지만 렌더링 부하를 분산
//    - OrderbookHeatmapEngine 인스턴스를 메인과 공유 (데이터 동기화 불필요)

using ScottPlot;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Upbit_Manager.Core.Orderbook;

namespace Upbit_Manager   // ✅ 루트 네임스페이스 (Form1과 동일)
{
    public sealed partial class HeatmapForm : Form   // ✅ Form 명시 상속
    {
        #region [ 필드 ]

        private readonly OrderbookHeatmapEngine _engine;
        private readonly FormsPlot _formsPlot;

        // 🔥 렌더링 쓰로틀 (500ms = 초당 2회)
        private const int RenderIntervalMs = 500;
        private System.Windows.Forms.Timer _renderTimer = null!;

        // 백그라운드 계산 결과
        private readonly object _resultLock = new();
        private RenderPayload? _pending = null;
        private bool _hasNew = false;

        // 백그라운드 계산 제어
        private readonly object _calcLock = new();
        private bool _isCalculating = false;
        private bool _calcRequested = false;

        // ScottPlot
        private ScottPlot.Plottables.Heatmap? _heatmapPlottable;

        #endregion

        #region [ 생성자 ]

        public HeatmapForm(OrderbookHeatmapEngine engine)
        {
            _engine = engine;

            // ── 폼 기본 설정 ──────────────────────────
            Text = "오더북 히트맵";
            Size = new System.Drawing.Size(1000, 600);
            MinimumSize = new System.Drawing.Size(600, 400);
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.Sizable;

            // ── FormsPlot 생성 ────────────────────────
            _formsPlot = new FormsPlot
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(_formsPlot);

            // ── 차트 초기 설정 ────────────────────────
            SetupPlot();

            // ── 렌더 타이머 (500ms) ───────────────────
            _renderTimer = new System.Windows.Forms.Timer { Interval = RenderIntervalMs };
            _renderTimer.Tick += (_, _) => TryRender();
            _renderTimer.Start();

            // ── 폼 닫기 → 숨기기로 재정의 ────────────
            FormClosing += (_, e) =>
            {
                e.Cancel = true;
                Hide();
            };

            // ── 마우스 우클릭 → 줌 리셋 ─────────────
            _formsPlot.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    _formsPlot.Plot.Axes.AutoScale();
                    _formsPlot.Refresh();
                }
            };
        }

        #endregion

        #region [ 차트 설정 ]

        private void SetupPlot()
        {
            var plot = _formsPlot.Plot;

            plot.Title("오더북 히트맵  |  노랑/흰색: 활성잔량  |  초록: 체결소멸  |  빨강: Spoofing의심");
            plot.XLabel("시간");
            plot.YLabel("가격 (KRW)");

            // X축 시간 포맷
            plot.Axes.DateTimeTicksBottom();
            var dtGen = new ScottPlot.TickGenerators.DateTimeAutomatic();
            dtGen.LabelFormatter = dt => dt.ToString("HH:mm");
            plot.Axes.Bottom.TickGenerator = dtGen;

            // 다크 테마 (히트맵 색상이 잘 보이도록)
            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1e1e1e");
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#252526");
            plot.Axes.Color(ScottPlot.Color.FromHex("#d4d4d4"));
        }

        #endregion

        #region [ 데이터 수신 (외부에서 호출) ]

        /// <summary>
        /// MainController에서 오더북 스냅샷 수신 시 호출
        /// 백그라운드에서 배열 계산 후 다음 렌더 타이머에서 반영
        /// </summary>
        public void PushSnapshot(OrderbookSnapshot snapshot)
        {
            // 엔진은 이미 MainController에서 ProcessSnapshot 완료된 상태
            // (엔진 공유이므로 여기서는 계산만 요청)
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

                data[row, col] = cell.State switch
                {
                    HeatmapCellState.FilledAndGone => 1.5,
                    HeatmapCellState.SpoofingSuspect => 2.0,
                    _ => Math.Clamp(cell.Volume / maxVol, 0.05, 0.99)
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
            if (!IsHandleCreated || !Visible) return;

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
                    _formsPlot.Refresh();
                }
                return;
            }

            // 🔥 기존 Heatmap 교체
            if (_heatmapPlottable != null)
                plot.Remove(_heatmapPlottable);

            _heatmapPlottable = plot.Add.Heatmap(payload.Data);

            _heatmapPlottable.Position = new CoordinateRect(
                payload.XMin,
                payload.XMax,
                payload.YMin,
                payload.YMax);

            _heatmapPlottable.Colormap = new ScottPlot.Colormaps.Viridis();
            _heatmapPlottable.FlipVertically = false;

            // Y축 자동 스케일 (가격 범위에 맞게)
            plot.Axes.SetLimitsY(payload.YMin, payload.YMax);

            _formsPlot.Refresh();
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