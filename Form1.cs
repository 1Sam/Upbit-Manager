using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Upbit_Manager.Controllers;
using Upbit_Manager.Core;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Services;
using Upbit_Manager.Services.Common;
using Upbit_Manager.Services.Upbit;
using Upbit_Manager.UI;
using Upbit_Manager.UI.Series;
using Upbit_Manager.Core;
using Upbit_Manager.Models.Upbit;
//using Upbit_Manager.UI;

namespace Upbit_Manager
{
    /// <summary>
    /// 업비트 매니저 메인 윈도우 폼입니다.
    /// 모든 UI 구성 요소와 컨트롤러 간의 이벤트를 중계하며 실시간 모니터링 및 알람 설정을 관리합니다.
    /// </summary>
    public partial class Form1 : Form
    {
        private readonly MainController _controller;
        private readonly AccountManager _accountManager;
        private readonly ChartManager _chartManager;
        private readonly UpbitRestService _upbitRestService;
        private readonly ExchangeRateService _rateService;

        private string _currentMarket = "KRW-ADA";
        private DateTime _lastFullUpdateTime = DateTime.MinValue;
        private bool _isApiValid = true;

        public Form1()
        {
            InitializeComponent();

            // 1. API 키 초기 설정 (저장된 키 로드)
            var startupKeys = ApiKeyStore.ReadKeys();
            if (startupKeys == null)
            {
                if (!ShowApiKeyDialog())
                {
                    MessageBox.Show("API 키가 필요합니다. 프로그램을 종료합니다.");
                    Application.Exit();
                    return;
                }
            }
            else
            {
                ApiConfig.AccessKey = startupKeys.Value.access;
                ApiConfig.SecretKey = startupKeys.Value.secret;
            }

            // 2. DataGridView 더블버퍼링 (깜빡임 방지)
            typeof(DataGridView).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(dataGridView1, true, null);

            // 3. 핵심 객체 초기화
            _accountManager = new AccountManager();
            _chartManager = new ChartManager(formsPlot1);
            _rateService = new ExchangeRateService();
            _upbitRestService = new UpbitRestService(ApiConfig.AccessKey, ApiConfig.SecretKey);

            // ⭐ 컨트롤러 생성 (모든 엔진 제어권 포함)
            _controller = new MainController(_upbitRestService, _rateService, _accountManager, _chartManager);

            // 4. 컨트롤러 -> UI 이벤트 연결
            _controller.OnExchangeRateUpdated = (rate) =>
            {
                this.InvokeIfRequired(() => UpdateRateLabel(rate));
            };

            // ⭐ 알람 통계 수치 업데이트 이벤트 연결
            _controller.OnVolumeStatsUpdated = (avg, threshold) =>
            {
                this.InvokeIfRequired(() => {
                    lblCurrentAvg.Text = $"현재 20분 평균: {avg:N0}";
                    lblTargetVol.Text = $"알람 기준량: {threshold:N0}";
                });
            };

            Logger.OnLogAdded += (logLine) =>
            {
                this.InvokeIfRequired(() => UpdateLogTextBox(logLine));
            };

            // 5. 폼 공통 이벤트 핸들러 등록
            this.FormClosing += Form1_FormClosing;
            this.toolStripStatusLabel1.Click += toolStripStatusLabel1_Click;
            this.toolStripStatusLabel1.DoubleClick += toolStripStatusLabel1_DoubleClick;

            // 6. UI 초기화 및 타이머 시작
            SetupDataGridView();
            InitLogDisplay();
            InitChartSeriesList();
            SetupTimers();
            SetupAlarmControlHandlers();

            // 7. 프로그램 초기 로직 실행
            InitProgram();
        }

        #region [초기화 및 시스템 설정]

        private void SetupTimers()
        {
            // 차트 UI 갱신 (100ms)
            var uiTimer = new System.Windows.Forms.Timer { Interval = 100 };
            uiTimer.Tick += (s, e) => _chartManager.UpdateUI();
            uiTimer.Start();

            // 데이터 갱신 (500ms)
            var mainDataTimer = new System.Windows.Forms.Timer { Interval = 500 };
            mainDataTimer.Tick += async (s, e) => await DoMainUpdate();
            mainDataTimer.Start();
        }

        private void InitChartSeriesList()
        {
            checkedListBox_ChartSeries.Items.Clear();
            foreach (var series in _chartManager.SeriesList)
            {
                checkedListBox_ChartSeries.Items.Add(series, series.DefaultOn);
            }

            checkedListBox_ChartSeries.ItemCheck += async (s, e) =>
            {
                if (checkedListBox_ChartSeries.Items[e.Index] is IChartSeries series)
                {
                    bool isChecked = (e.NewValue == CheckState.Checked);
                    series.IsVisible = isChecked;

                    if (series.Source == ExchangeSource.Binance && series.Type == SeriesType.PriceLine)
                    {
                        await _controller.ToggleBinanceService(isChecked);
                    }

                    _chartManager.UpdateUI();
                }
            };
        }

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
        }

        /// <summary>
        /// 알람 설정 UI 컨트롤들의 이벤트 핸들러를 설정합니다.
        /// </summary>
        private void SetupAlarmControlHandlers()
        {
            // 배수 설정 변경 시
            numVolMultiplier.ValueChanged += (s, e) => {
                Logger.Log($"[설정] 거래량 감시 배수 변경: {numVolMultiplier.Value}배");
            };

            // 쿨타임 트랙바 변경 시
            trkbCooldown.Scroll += (s, e) => {
                if (lblCooldownValue != null)
                    lblCooldownValue.Text = $"{trkbCooldown.Value}초";
            };

            // 알람 활성화 체크박스
            chkAlarmEnable.CheckedChanged += (s, e) => {
                string status = chkAlarmEnable.Checked ? "활성화" : "비활성화";
                Logger.Log($"[설정] 실시간 알람 엔진 {status}");
            };
        }

        #endregion

        #region [데이터 갱신 루프]

        private async void InitProgram()
        {
            toolStripStatusLabel1.Text = "API 인증 확인 중...";
            toolStripStatusLabel1.ForeColor = Color.Black;

            string result = await _controller.InitializeProgram();

            if (result == "SUCCESS")
            {
                _isApiValid = true;
                toolStripStatusLabel1.Text = "API 인증 및 초기화 성공";
                toolStripStatusLabel1.ForeColor = Color.Blue;
            }
            else
            {
                _isApiValid = false;
                toolStripStatusLabel1.Text = result;
                toolStripStatusLabel1.ForeColor = Color.Red;
                toolStripStatusLabel1.ToolTipText = "클릭하면 현재 IP를 복사하고 업비트 관리 페이지로 이동합니다.";
            }
        }

        private async Task DoMainUpdate()
        {
            if (!_isApiValid) return;

            if ((DateTime.Now - _lastFullUpdateTime).TotalSeconds >= 2)
            {
                try
                {
                    string assetJson = await _upbitRestService.GetAccountsJsonAsync();

                    if (assetJson.StartsWith("ERROR_MSG:"))
                    {
                        _isApiValid = false;
                        this.InvokeIfRequired(() =>
                        {
                            toolStripStatusLabel1.Text = assetJson.Replace("ERROR_MSG:", "");
                            toolStripStatusLabel1.ForeColor = Color.Red;
                        });
                        return;
                    }

                    _accountManager.UpdateAssetsFromJson(assetJson);

                    string orderJson = await _upbitRestService.GetOpenOrdersJsonAsync();
                    if (!orderJson.StartsWith("ERROR_MSG:"))
                    {
                        _accountManager.UpdateOpenOrders(orderJson);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Update Error: {ex.Message}");
                }
                _lastFullUpdateTime = DateTime.Now;
            }

            UpdateAssetLabels();
            UpdateChartLayers();
        }

        private void UpdateChartLayers()
        {
            if (_accountManager == null || _chartManager == null) return;

            var myOrders = _accountManager.GetOpenOrdersByMarket(_currentMarket);
            _chartManager.PushData(SeriesType.OpenOrder, myOrders, ExchangeSource.Upbit);

            var asset = _accountManager.GetAssetByMarket(_currentMarket);
            if (asset != null)
            {
                var payload = new AccountInfoPayload(asset.AvgBuyPrice, asset.ProfitRate);
                _chartManager.PushData(SeriesType.AvgPriceLine, payload, ExchangeSource.Upbit);
            }
            else
            {
                _chartManager.PushData(SeriesType.AvgPriceLine, new AccountInfoPayload(0, 0), ExchangeSource.Upbit);
            }
        }

        private void UpdateAssetLabels()
        {
            if (!this.IsHandleCreated) return;

            this.InvokeIfRequired(() =>
            {
                _accountManager.MyAssets.TryGetValue("KRW", out var krw);
                string currentSymbol = _currentMarket?.Split('-').LastOrDefault() ?? "ADA";
                _accountManager.MyAssets.TryGetValue(currentSymbol, out var coinAsset);

                double totalEval = _accountManager.GetTotalEvaluationAmount();
                double pureCash = krw?.TotalInventory ?? 0;

                lblCashTotal.Text = $"총 보유자산: {totalEval + pureCash:N0} KRW (평가: {totalEval:N0})";
                lblCashAvailable.Text = $"Cash : {pureCash:N0} KRW";

                if (coinAsset != null)
                {
                    lblAdaBalance.Text = $"보유 수량: {coinAsset.TotalInventory:N4} {currentSymbol}";
                    lblAdaAvg.Text = $"매수평단: {coinAsset.AvgBuyPrice:N2} KRW";
                    lblAdaProfitRate.Text = $"수익률: {coinAsset.ProfitRate:N2}%";
                    lblAdaProfitLoss.Text = $"평가손익: {coinAsset.ProfitLoss:N0} KRW";

                    var color = coinAsset.ProfitRate >= 0 ? Color.Red : Color.Blue;
                    lblAdaProfitRate.ForeColor = color;
                    lblAdaProfitLoss.ForeColor = color;
                }

                SyncAssetDataGridView();
            });
        }

        private void SyncAssetDataGridView()
        {
            var displayAssets = _accountManager.MyAssets.Values
                .Where(a => a.Symbol != "KRW")
                .OrderByDescending(a => a.EvaluationAmount).ToList();

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
                row.Cells[5].Value = $"{asset.ProfitRate:N2}%";
                row.Cells[6].Value = $"{asset.ProfitLoss:N0}";

                row.DefaultCellStyle.ForeColor = asset.ProfitRate >= 0 ? Color.Red : Color.Blue;
            }
        }

        #endregion

        #region [컴포넌트 이벤트 핸들러]

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

        private async void btnExecuteBatch_Click(object sender, EventArgs e)
        {
            var asset = _accountManager.GetAssetByMarket(_currentMarket);
            double currentPrice = asset?.CurrentPrice ?? 0;

            if (currentPrice <= 0)
            {
                MessageBox.Show("현재가를 불러올 수 없습니다.");
                return;
            }

            if (MessageBox.Show($"{_currentMarket} 종목을 현재가 {currentPrice:N0}원 기준으로 일괄 매수(그리드) 하시겠습니까?", "일괄 매수 확인", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                await _controller.ExecuteBatchPurchase(currentPrice);
            }
        }

        private void btnApplySimulation_Click(object sender, EventArgs e)
        {
            if (!double.TryParse(txtSimulateCash.Text, out double addCash) || addCash <= 0)
            {
                MessageBox.Show("추가 투입할 금액을 숫자로 입력해주세요.");
                return;
            }

            var asset = _accountManager.GetAssetByMarket(_currentMarket);
            if (asset == null || asset.TotalInventory <= 0)
            {
                MessageBox.Show("보유 중인 자산이 없어 계산이 불가능합니다.");
                return;
            }

            double currentPrice = asset.CurrentPrice;
            if (currentPrice <= 0) return;

            double addedQty = addCash / currentPrice;
            double expectedAvg = (asset.TotalBuyAmount + addCash) / (asset.TotalInventory + addedQty);

            _chartManager.PushData(SeriesType.SimulatedAvgPriceLine, expectedAvg, ExchangeSource.Upbit);
            lblExpectedAvg.Text = $"예상 평단: {expectedAvg:N2}";

            for (int i = 0; i < checkedListBox_ChartSeries.Items.Count; i++)
            {
                if (checkedListBox_ChartSeries.Items[i] is UpbitSimulateSeries)
                {
                    checkedListBox_ChartSeries.SetItemChecked(i, true);
                    break;
                }
            }
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            ShowApiKeyDialog("API Key 수정");
        }

        private void flowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _chartManager.ResetTimelineOnly();
            formsPlot1.Focus();
        }

        private void UpdateRateLabel(double rate)
        {
            toolStripStatusLabel2.Text = $"현재 환율: {rate:N2} KRW/USD";
        }

        private void UpdateLogTextBox(string newLog)
        {
            var lines = textBox1.Lines.ToList();
            lines.Insert(0, newLog);
            if (lines.Count > 20) lines = lines.Take(20).ToList();
            textBox1.Lines = lines.ToArray();
        }

        private void InitLogDisplay()
        {
            textBox1.Clear();
            var oldLogs = Logger.GetLastLogs(10);

            if (oldLogs.Count > 0)
            {
                textBox1.Lines = oldLogs.ToArray();
                Logger.Log("이전 로그 기록을 불러왔습니다.");
            }
            else
            {
                Logger.Log("새로운 로그 세션을 시작합니다.");
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _ = _controller.ToggleBinanceService(false);
            Logger.Log("시스템을 종료합니다.");
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e) => HandleStatusLabelInteraction();
        private void toolStripStatusLabel1_DoubleClick(object sender, EventArgs e) => HandleStatusLabelInteraction();

        private void HandleStatusLabelInteraction()
        {
            string statusText = toolStripStatusLabel1.Text;
            var match = System.Text.RegularExpressions.Regex.Match(statusText, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");

            if (!match.Success) return;

            Clipboard.SetText(match.Value);
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://upbit.com/mypage/open_api_management",
                    UseShellExecute = true
                });
                Logger.Log($"IP 복사 완료 및 업비트 관리 페이지 오픈: {match.Value}");
            }
            catch { }
        }

        private bool ShowApiKeyDialog(string title = "API Key 입력")
        {
            using var dlg = new Form
            {
                Text = title,
                Size = new Size(420, 180),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };

            var txtA = new TextBox { Location = new Point(110, 10), Width = 280 };
            var txtS = new TextBox { Location = new Point(110, 40), Width = 280, UseSystemPasswordChar = true };
            var btnOk = new Button { Text = "저장", Location = new Point(110, 80), DialogResult = DialogResult.OK };
            var btnCan = new Button { Text = "취소", Location = new Point(200, 80), DialogResult = DialogResult.Cancel };

            dlg.Controls.AddRange(new Control[] {
                new Label { Text = "Access Key:", Location = new Point(10, 10) }, txtA,
                new Label { Text = "Secret Key:", Location = new Point(10, 40) }, txtS,
                btnOk, btnCan
            });

            if (dlg.ShowDialog() != DialogResult.OK) return false;

            string a = txtA.Text.Trim();
            string s = txtS.Text.Trim();
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(s)) return false;

            ApiKeyStore.SaveKeys(a, s);
            ApiConfig.AccessKey = a;
            ApiConfig.SecretKey = s;
            return true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (cmbAlarmSound != null && cmbAlarmSound.Items.Count > 0)
            {
                cmbAlarmSound.SelectedIndex = 0;
            }
        }

        #endregion
    }

    /// <summary>
    /// UI 컨트롤 접근을 위한 확장 메서드입니다.
    /// </summary>
    public static class ControlExtensions
    {
        public static void InvokeIfRequired(this Control control, Action action)
        {
            if (control.InvokeRequired) control.Invoke(action);
            else action();
        }
    }
}