using Upbit_Manager.Controllers;
using Upbit_Manager.Core;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Services;
using Upbit_Manager.Services.Common;
using Upbit_Manager.Services.Upbit;
using Upbit_Manager.UI;
using Upbit_Manager.UI.Series;

namespace Upbit_Manager
{
    public partial class Form1 : Form
    {
        private readonly MainController _controller;
        private readonly AccountManager _accountManager;
        private readonly ChartManager _chartManager;
        private readonly UpbitEngine _upbitEngine;
        private readonly BinanceEngine _binanceEngine;
        private readonly UpbitRestService _upbitRestService;
        private readonly ExchangeRateService _rateService;

        private string _currentMarket = "KRW-ADA";
        private DateTime _lastFullUpdateTime = DateTime.MinValue;

        public Form1()
        {
            InitializeComponent();

            // ── API 키 초기 설정 ───────────────────────────────────────
            var startupKeys = ApiKeyStore.ReadKeys();
            if (startupKeys == null)
            {
                if (!ShowApiKeyDialog())
                {
                    MessageBox.Show("API 키가 필요합니다. 프로그램을 종료합니다.",
                                    "종료", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Application.Exit();
                    return;
                }
            }
            else
            {
                ApiConfig.AccessKey = startupKeys.Value.access;
                ApiConfig.SecretKey = startupKeys.Value.secret;
            }

            // DataGridView 더블버퍼링 (깜빡임 방지)
            typeof(DataGridView).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(dataGridView1, true, null);

            // ── 핵심 객체 초기화 ───────────────────────────────────────
            _accountManager = new AccountManager();
            _chartManager = new ChartManager(formsPlot1);
            _rateService = new ExchangeRateService();
            _upbitEngine = new UpbitEngine(_accountManager);
            _binanceEngine = new BinanceEngine();
            _upbitRestService = new UpbitRestService(ApiConfig.AccessKey, ApiConfig.SecretKey);
            _controller = new MainController(_upbitRestService, _rateService,
                                                   _accountManager, _chartManager);

            // ── 이벤트 연결 ───────────────────────────────────────────
            _upbitEngine.OnTradeUpdated += (price, vol, side, market) =>
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(() => {
                        _controller.HandleRealtimeTrade(price, vol, side, market);

                        // 핵심: market이 USDT일 때 ChartManager의 새 메서드 호출!
                        if (market == "KRW-USDT")
                            _chartManager.UpdateUsdtPrice(price);
                    });
                }
                else
                {
                    _controller.HandleRealtimeTrade(price, vol, side, market);
                    if (market == "KRW-USDT") _chartManager.UpdateUsdtPrice(price);
                }
            };

            // 바이낸스 실시간 가격 → ChartManager (항상 수집, 표시는 IsVisible로 제어)
            _binanceEngine.OnPriceUpdated += (krwPrice) =>
            {
                if (this.InvokeRequired)
                    this.Invoke(() => _chartManager.UpdateBinancePrice(krwPrice));
                else
                    _chartManager.UpdateBinancePrice(krwPrice);
            };

            _controller.OnExchangeRateUpdated = (rate) =>
            {
                if (statusStrip1.InvokeRequired)
                    statusStrip1.Invoke(() => UpdateRateLabel(rate));
                else
                    UpdateRateLabel(rate);
            };

            Logger.OnLogAdded += (logLine) =>
            {
                if (this.InvokeRequired) this.Invoke(() => UpdateLogTextBox(logLine));
                else UpdateLogTextBox(logLine);
            };

            this.FormClosing += Form1_FormClosing;
            toolStripStatusLabel1.Click += toolStripStatusLabel1_Click;
            toolStripStatusLabel1.DoubleClick += toolStripStatusLabel1_DoubleClick;

            // ── UI 초기화 ──────────────────────────────────────────────
            SetupDataGridView();
            InitLogDisplay();

            // 체크리스트 초기화 (chkBinance 대체)
            InitChartSeriesList();

            // ── 타이머 ─────────────────────────────────────────────────
            // UI 갱신 (100ms)
            var uiTimer = new System.Windows.Forms.Timer { Interval = 300 };
            uiTimer.Tick += (s, e) =>
            {
                _chartManager.UpdateUI();
                UpdateAssetLabels();
            };
            uiTimer.Start();

            // 데이터 갱신 (500ms)
            var mainDataTimer = new System.Windows.Forms.Timer { Interval = 500 };
            mainDataTimer.Tick += MainUpdateTimer_Tick; // ← flowToolStripMenuItem_DoubleClick 에서 변경
            mainDataTimer.Start();

            InitProgram();
        }

        // ── 체크리스트 초기화 ──────────────────────────────────────────

        /// <summary>
        /// ChartManager에 등록된 시리즈 목록을 CheckedListBox에 자동 바인딩합니다.
        /// 새 시리즈는 ChartManager._seriesList에만 추가하면 여기도 자동 반영됩니다.
        /// </summary>
        private void InitChartSeriesList()
        {
            checkedListBox_ChartSeries.Items.Clear();

            foreach (var series in _chartManager.SeriesList)
                checkedListBox_ChartSeries.Items.Add(series, series.DefaultOn);

            checkedListBox_ChartSeries.ItemCheck += OnChartSeriesItemCheck;
        }

        /// <summary>
        /// 체크 상태 변경 시 MainController를 통해 시리즈 표시 여부를 토글합니다.
        /// 바이낸스 항목이 켜지면 히스토리 로드 + 엔진 시작도 함께 처리합니다.
        /// </summary>
        private async void OnChartSeriesItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (checkedListBox_ChartSeries.Items[e.Index] is not IChartSeries series) return;

            bool isChecked = e.NewValue == CheckState.Checked;

            // 1. 차트 표시 여부 토글
            _controller.ToggleSeries(series.Source, series.Type, isChecked);

            // 2. 바이낸스 가격선 전용 처리 (엔진 시작/중지)
            if (series.Source == ExchangeSource.Binance && series.Type == SeriesType.PriceLine)
            {
                await HandleBinanceToggle(isChecked);
            }
        }

        /// <summary>
        /// 바이낸스 엔진 시작/중지 및 히스토리 로드를 처리합니다.
        /// 기존 chkBinance_CheckedChanged 로직을 이전한 것입니다.
        /// </summary>
        private async Task HandleBinanceToggle(bool isChecked)
        {
            if (isChecked)
            {
                // 환율 가져오기
                double rate = await _rateService.GetUsdToKrwAsync();

                // 과거 히스토리 먼저 로드
                string currentMarket = _controller.GetSelectedMarket();
                var history = await _controller.GetBinanceHistoryAsync(currentMarket, rate);
                _chartManager.UpdateBinanceHistory(history);

                // 실시간 엔진 시작
                await _binanceEngine.StartAsync(currentMarket, rate);

                Logger.Log($"바이낸스 시세 연동 시작 (적용환율: {rate:N2})");
            }
            else
            {
                // 엔진 중지 + 버퍼 초기화
                await _binanceEngine.StopAsync();
                _chartManager.ClearBinanceSeries();

                Logger.Log("바이낸스 시세 연동 종료");
            }
        }

        // ── API 키 입력 다이얼로그 ─────────────────────────────────────

        /// <summary>API 키 입력 다이얼로그를 표시하고 저장합니다.</summary>
        private bool ShowApiKeyDialog(string title = "API Key 입력 (초기설정)")
        {
            using var dlg = new Form
            {
                Text = title,
                Size = new System.Drawing.Size(420, 180),
                StartPosition = FormStartPosition.CenterParent
            };

            var txtA = new TextBox { Location = new System.Drawing.Point(110, 10), Width = 280 };
            var txtS = new TextBox
            {
                Location = new System.Drawing.Point(110, 40),
                Width = 280,
                UseSystemPasswordChar = true
            };
            var btnOk = new Button
            {
                Text = "저장",
                Location = new System.Drawing.Point(110, 80),
                DialogResult = DialogResult.OK
            };
            var btnCan = new Button
            {
                Text = "취소",
                Location = new System.Drawing.Point(200, 80),
                DialogResult = DialogResult.Cancel
            };

            dlg.Controls.AddRange(new Control[]
            {
                new Label { Text = "Access Key:", Location = new System.Drawing.Point(10, 10) }, txtA,
                new Label { Text = "Secret Key:", Location = new System.Drawing.Point(10, 40) }, txtS,
                btnOk, btnCan
            });
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCan;

            if (dlg.ShowDialog() != DialogResult.OK) return false;

            ApiKeyStore.SaveKeys(txtA.Text.Trim(), txtS.Text.Trim());
            ApiConfig.AccessKey = txtA.Text.Trim();
            ApiConfig.SecretKey = txtS.Text.Trim();
            return true;
        }

        // ── 나머지 기존 코드 (변경 없음) ──────────────────────────────



        #region [상태 표시줄 및 IP 복사]

        private void toolStripStatusLabel1_Click(object sender, EventArgs e) => HandleStatusLabelInteraction();
        private void toolStripStatusLabel1_DoubleClick(object sender, EventArgs e) => HandleStatusLabelInteraction();

        private void HandleStatusLabelInteraction()
        {
            string statusText = toolStripStatusLabel1.Text;
            var match = System.Text.RegularExpressions.Regex
                .Match(statusText, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");
            if (!match.Success) return;

            Clipboard.SetText(match.Value);
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://upbit.com/mypage/open_api_management",
                    UseShellExecute = true
                });
            }
            catch { }

            string originalText = toolStripStatusLabel1.Text;
            toolStripStatusLabel1.Text = $"[복사완료 & 페이지오픈] {match.Value}";

            var timer = new System.Windows.Forms.Timer { Interval = 2000 };
            timer.Tick += (s, ev) =>
            {
                toolStripStatusLabel1.Text = originalText;
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }

        #endregion

        #region [자산 관리 및 그리드 업데이트]

        private void SetupDataGridView()
        {
            dataGridView1.Columns.Clear();
            dataGridView1.Columns.Add("CoinName", "자산");
            dataGridView1.Columns.Add("Balance", "수량");
            dataGridView1.Columns.Add("AvgPrice", "평단");
            dataGridView1.Columns.Add("BuyAmount", "매수금");
            dataGridView1.Columns.Add("EvalAmount", "평가금");
            dataGridView1.Columns.Add("ProfitRate", "수익%");
            dataGridView1.Columns.Add("ProfitLoss", "손익");

            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AllowUserToAddRows = false;

            dataGridView1.CellDoubleClick += async (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var cell = dataGridView1.Rows[e.RowIndex].Cells["CoinName"].Value;
                if (cell == null) return;
                string m = cell.ToString();
                await _controller.ChangeMarket(m.Contains("-") ? m : $"KRW-{m}");
            };
        }

        private void UpdateAssetLabels()
        {
            if (!this.IsHandleCreated) return;
            this.Invoke(() =>
            {
                _accountManager.MyAssets.TryGetValue("KRW", out var krw);
                string currentSymbol = _currentMarket?.Split('-').LastOrDefault() ?? "ADA";
                _accountManager.MyAssets.TryGetValue(currentSymbol, out var coinAsset);

                double totalEval = _accountManager.GetTotalEvaluationAmount();
                double pureCash = krw?.TotalInventory ?? 0;

                lblCashTotal.Text = $"총 보유자산: {totalEval + pureCash:N0} KRW (평가: {totalEval:N0})";
                lblCashAvailable.Text = $"Cash : {krw?.TotalInventory:N0} KRW";
                lblCashLocked.Text = $"in BID : {(totalEval + pureCash - (krw?.TotalInventory ?? 0)):N0} KRW";

                if (coinAsset != null)
                {
                    lblAdaBalance.Text = $"보유 수량: {coinAsset.TotalInventory:N4} {currentSymbol}";
                    lblAdaLocked.Text = $"매도 대기: - {currentSymbol}";
                    lblAdaInventory.Text = $"총 보유량: {coinAsset.TotalInventory:N4} {currentSymbol}";
                    lblAdaAvg.Text = $"매수평단: {coinAsset.AvgBuyPrice:N2} KRW";
                    lblAdaProfitRate.Text = $"수익률: {coinAsset.ProfitRate:N2}%";
                    lblAdaProfitLoss.Text = $"평가손익: {coinAsset.ProfitLoss:N0} KRW";

                    var color = coinAsset.ProfitRate >= 0
                        ? System.Drawing.Color.Red
                        : System.Drawing.Color.Blue;
                    lblAdaProfitRate.ForeColor = color;
                    lblAdaProfitLoss.ForeColor = color;
                }

                var displayAssets = _accountManager.MyAssets.Values
                    .Where(a => a.Symbol != "KRW")
                    .OrderByDescending(a => a.EvaluationAmount)
                    .ToList();

                dataGridView1.SuspendLayout();
                if (dataGridView1.Rows.Count != displayAssets.Count)
                {
                    dataGridView1.Rows.Clear();
                    foreach (var _ in displayAssets) dataGridView1.Rows.Add();
                }

                for (int i = 0; i < displayAssets.Count; i++)
                {
                    var asset = displayAssets[i];
                    var row = dataGridView1.Rows[i];
                    row.Cells[0].Value = asset.Symbol;
                    row.Cells[1].Value = $"{asset.TotalInventory:N4}";
                    row.Cells[2].Value = $"{asset.AvgBuyPrice:N2}";
                    row.Cells[3].Value = $"{asset.TotalBuyAmount:N0}";
                    row.Cells[4].Value = $"{asset.EvaluationAmount:N0}";
                    row.Cells[5].Value = $"{asset.ProfitRate:+0.00;-0.00;0.00}%";
                    row.Cells[6].Value = $"{asset.ProfitLoss:+N0;-N0;0}";

                    row.DefaultCellStyle.ForeColor =
                        asset.ProfitRate > 0 ? System.Drawing.Color.Red :
                        asset.ProfitRate < 0 ? System.Drawing.Color.Blue :
                                               System.Drawing.Color.Black;
                }
                dataGridView1.ResumeLayout();
            });
        }

        #endregion

        #region [시스템 제어 및 로그]

        private async void InitProgram()
        {
            toolStripStatusLabel1.Text = "API 인증 확인 중...";
            var authResult = await _upbitEngine.CheckAuthAsync();
            toolStripStatusLabel1.Text = authResult.message;
            toolStripStatusLabel1.ForeColor = authResult.isSuccess
                ? System.Drawing.Color.Blue
                : System.Drawing.Color.Red;

            if (authResult.isSuccess)
            {
                await _controller.InitializeProgram();
                string[] myMarkets = _accountManager.GetSubscribingMarkets();
                if (!myMarkets.Contains("KRW-ADA"))
                    myMarkets = myMarkets.Append("KRW-ADA").ToArray();
                // Ensure KRW-USDT is subscribed so the USDT price line receives realtime ticks
                if (!myMarkets.Contains("KRW-USDT"))
                    myMarkets = myMarkets.Append("KRW-USDT").ToArray();
                Logger.Log($"WebSocket subscribing markets: {string.Join(',', myMarkets)}");
                _ = Task.Run(() => _upbitEngine.RunWebSocketLoopAsync(myMarkets));
            }
        }

        private void InitLogDisplay() => textBox1.Lines = Logger.GetLastLogs(20).ToArray();
        private void UpdateRateLabel(double rate)
        {
            toolStripStatusLabel2.Text = $"현재 환율: {rate:N2} KRW/USD";
            toolStripStatusLabel2.ForeColor = Color.DarkBlue;
        }

        private void UpdateLogTextBox(string newLog)
        {
            var lines = textBox1.Lines.ToList();
            lines.Insert(0, newLog);
            if (lines.Count > 20) lines = lines.Take(20).ToList();
            textBox1.Lines = lines.ToArray();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _upbitEngine.StopWebSocket();
            Logger.Log("프로그램을 종료합니다.");
        }

        #endregion

        #region [타이머 및 차트 갱신]


        private async Task RefreshHeavyData()
        {
            try
            {
                string assetJson = await _upbitRestService.GetAccountsJsonAsync();
                _accountManager.UpdateAssetsFromJson(assetJson);

                string orderJson = await _upbitRestService.GetOpenOrdersJsonAsync();
                _accountManager.UpdateOpenOrders(orderJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"데이터 갱신 오류: {ex.Message}");
            }
        }

        private void UpdateChartLayers()
        {
            if (_accountManager == null || _chartManager == null) return;
            var myOrders = _accountManager.GetOpenOrdersByMarket(_currentMarket);
            _chartManager.UpdateMyOrders(myOrders);
        }

        #endregion

        #region [DataGridView 이벤트]

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var cellValue = dataGridView1.Rows[e.RowIndex].Cells["CoinName"].Value;
            if (cellValue == null) return;

            string symbol = cellValue.ToString();
            _currentMarket = symbol.Contains("-") ? symbol : $"KRW-{symbol}";

            _ = _controller.ChangeMarket(_currentMarket);
            UpdateChartLayers();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e) { }

        #endregion
        private void Form1_Load(object sender, EventArgs e) { }
        private void textBox1_TextChanged(object sender, EventArgs e) { }



        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            ShowApiKeyDialog("API Key 입력");
        }


        private void toolStripComboBox1_Click(object sender, EventArgs e)
        {

        }

        private void checkedListBox_ChartSeries_SelectedIndexChanged(object sender, EventArgs e)
        {

        }



        // 타이머 전용 핸들러 (신규 추가)
        private async void MainUpdateTimer_Tick(object sender, EventArgs e)
        {
            await DoMainUpdate();
        }

        // 공통 로직 (신규 추가)
        private async Task DoMainUpdate()
        {
            if ((DateTime.Now - _lastFullUpdateTime).TotalSeconds >= 2)
            {
                await RefreshHeavyData();
                _lastFullUpdateTime = DateTime.Now;
            }
            UpdateAssetLabels();
            UpdateChartLayers();
        }

        private async void flowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoMainUpdate();
        }

    }
}