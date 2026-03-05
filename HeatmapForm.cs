// ✅ [수정] HeatmapForm.cs
// 🔥 히스토리 배치 수신: PushHistorySnapshot() → 엔진에만 주입, 렌더링 스킵
// 🔥 히스토리 완료: OnHistoryCompleted() → 렌더링 1회 강제 실행
// 🔥 X축: 6시간 범위 표시

using ScottPlot;
using ScottPlot.WinForms;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Upbit_Manager.Core;
using Upbit_Manager.Core.Orderbook;

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

        // 🔥 사용자 수동줌 추적
        private bool _userManualZoom = false;

        // 🔥 히스토리 로딩 중 여부 (렌더링 타이머 차단용)
        private volatile bool _isLoadingHistory = false;

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

            _formsPlot = new FormsPlot { Dock = DockStyle.Fill };
            Controls.Add(_formsPlot);

            SetupPlot();

            _renderTimer = new System.Windows.Forms.Timer { Interval = RenderIntervalMs };
            _renderTimer.Tick += (_, _) => TryRender();
            _renderTimer.Start();

            FormClosing += (_, e) =>
            {
                e.Cancel = true;
                Hide();
            };

            // 우클릭 → 줌 리셋
            _formsPlot.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    _userManualZoom = false;
                    lock (_resultLock)
                    {
                        if (_pending != null) _hasNew = true;
                    }
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

            // 🔥 초기 X축: 현재 기준 과거 6시간 ~ 미래 2분
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

        #region [ 데이터 수신 ]

        /// <summary>
        /// 실시간 스냅샷 수신 (MainController → 여기)
        /// 엔진 처리 + 렌더링 계산 요청
        /// </summary>
        public void PushSnapshot(OrderbookSnapshot snapshot)
        {
            if (_isLoadingHistory) return; // 히스토리 로딩 중엔 실시간 스킵

            _engine.ProcessSnapshot(snapshot);
            RequestBackgroundCalc();
        }

        /// <summary>
        /// 🔥 히스토리 스냅샷 수신 (MMFBridgeService.OnHistorySnapshotReceived)
        /// 렌더링 없이 엔진에만 데이터 주입
        /// </summary>
        public void PushHistorySnapshot(OrderbookSnapshot snapshot)
        {
            _isLoadingHistory = true;
            _engine.ProcessSnapshot(snapshot);
            // 렌더링 요청 없음 → UI 블로킹 없음
        }

        /// <summary>
        /// 🔥 히스토리 소진 완료 콜백 (MMFBridgeService.OnHistoryLoadCompleted)
        /// 렌더링 1회 강제 실행
        /// </summary>
        public void OnHistoryCompleted()
        {
            _isLoadingHistory = false;
            Logger.Log("[Heatmap] 히스토리 로드 완료 → 렌더링 시작");

            // 백그라운드에서 배열 계산 후 렌더링
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

                    // 체결된 흔적은 아주 밝은 '하늘색' 계열로 인지되도록 높은 값 부여
                    HeatmapCellState.FilledAndGone => 1.5,

                    // 스푸핑 의심은 '가장 밝은' 값으로 고정
                    HeatmapCellState.SpoofingSuspect => 2.0,

                    // 일반 물량은 0.0 ~ 1.0 사이 (검정~흰색)
                    _ => Math.Clamp(cell.Volume / maxVol, 0.05, 0.99)
                    //HeatmapCellState.FilledAndGone => 1.5,
                    //HeatmapCellState.SpoofingSuspect => 2.0,
                    //_ => Math.Clamp(cell.Volume / maxVol, 0.05, 0.99)
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
            // 🔥 히스토리 로딩 중엔 렌더링 타이머 스킵
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
                    if (Visible) _formsPlot.Refresh();
                }
                return;
            }

            if (_heatmapPlottable != null)
                plot.Remove(_heatmapPlottable);

            _heatmapPlottable = plot.Add.Heatmap(payload.Data);

            _heatmapPlottable.Position = new CoordinateRect(
                payload.XMin,
                payload.XMax,
                payload.YMin,
                payload.YMax);

            //기존 보라색에 배경에서 아래의 눈에 잘 띄는 색상으로 변경
            //_heatmapPlottable.Colormap = new ScottPlot.Colormaps.Viridis();

            // TryRender 메서드 내부 수정
            var colorList = new ScottPlot.Color[]
            {
                ScottPlot.Color.FromHex("#000000"), // 물량 없음: 검정
                ScottPlot.Color.FromHex("#444444"), // 아주 적은 물량: 짙은 회색
                ScottPlot.Color.FromHex("#0077FF"), // 중간 물량: 파랑
                ScottPlot.Color.FromHex("#FFFFFF")  // 대량 물량(벽): 흰색 (가장 눈에 띔)
            };

            _heatmapPlottable.Colormap = new ScottPlot.Colormaps.Custom(colorList);


            _heatmapPlottable.FlipVertically = false;

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
            lock (_resultLock)
            {
                if (_pending != null) _hasNew = true;
            }
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