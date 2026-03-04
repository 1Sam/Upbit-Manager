namespace Upbit_Manager
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            formsPlot1 = new ScottPlot.WinForms.FormsPlot();
            lblCashTotal = new Label();
            lblAdaBalance = new Label();
            lblAdaLocked = new Label();
            lblAdaInventory = new Label();
            lblAdaAvg = new Label();
            lblCashLocked = new Label();
            lblCashAvailable = new Label();
            lblAdaProfitRate = new Label();
            lblAdaProfitLoss = new Label();
            lblTotalEval = new Label();
            lblTotalBuy = new Label();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            toolStripStatusLabel2 = new ToolStripStatusLabel();
            textBox1 = new TextBox();
            groupBox1 = new GroupBox();
            groupBox2 = new GroupBox();
            dataGridView1 = new DataGridView();
            checkedListBox_ChartSeries = new CheckedListBox();
            menuStrip1 = new MenuStrip();
            toolStripMenuItem2 = new ToolStripMenuItem();
            flowToolStripMenuItem = new ToolStripMenuItem();
            txtSimulateCash = new TextBox();
            btnApplySimulation = new Button();
            chkShowSimulateLine = new CheckBox();
            lblExpectedAvg = new Label();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            groupBox3 = new GroupBox();
            tabPage2 = new TabPage();
            grpVolumeAlarm = new GroupBox();
            lblCooldownValue = new Label();
            label3 = new Label();
            label2 = new Label();
            label1 = new Label();
            trkbCooldown = new TrackBar();
            cmbAlarmSound = new ComboBox();
            lblTargetVol = new Label();
            lblCurrentAvg = new Label();
            numVolMultiplier = new NumericUpDown();
            chkAlarmEnable = new CheckBox();
            tsHeatmap = new ToolStripMenuItem();
            statusStrip1.SuspendLayout();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            menuStrip1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            groupBox3.SuspendLayout();
            tabPage2.SuspendLayout();
            grpVolumeAlarm.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trkbCooldown).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numVolMultiplier).BeginInit();
            SuspendLayout();
            // 
            // formsPlot1
            // 
            formsPlot1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            formsPlot1.DisplayScale = 1F;
            formsPlot1.Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
            formsPlot1.Location = new Point(12, 27);
            formsPlot1.Name = "formsPlot1";
            formsPlot1.Size = new Size(868, 692);
            formsPlot1.TabIndex = 0;
            // 
            // lblCashTotal
            // 
            lblCashTotal.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblCashTotal.AutoSize = true;
            lblCashTotal.Location = new Point(3, 19);
            lblCashTotal.Name = "lblCashTotal";
            lblCashTotal.Size = new Size(72, 15);
            lblCashTotal.TabIndex = 1;
            lblCashTotal.Text = "lblCashTotal";
            // 
            // lblAdaBalance
            // 
            lblAdaBalance.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaBalance.AutoSize = true;
            lblAdaBalance.Location = new Point(911, 448);
            lblAdaBalance.Name = "lblAdaBalance";
            lblAdaBalance.Size = new Size(82, 15);
            lblAdaBalance.TabIndex = 1;
            lblAdaBalance.Text = "lblAdaBalance";
            // 
            // lblAdaLocked
            // 
            lblAdaLocked.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaLocked.AutoSize = true;
            lblAdaLocked.Location = new Point(911, 476);
            lblAdaLocked.Name = "lblAdaLocked";
            lblAdaLocked.Size = new Size(79, 15);
            lblAdaLocked.TabIndex = 1;
            lblAdaLocked.Text = "lblAdaLocked";
            // 
            // lblAdaInventory
            // 
            lblAdaInventory.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaInventory.AutoSize = true;
            lblAdaInventory.Location = new Point(911, 509);
            lblAdaInventory.Name = "lblAdaInventory";
            lblAdaInventory.Size = new Size(91, 15);
            lblAdaInventory.TabIndex = 1;
            lblAdaInventory.Text = "lblAdaInventory";
            // 
            // lblAdaAvg
            // 
            lblAdaAvg.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaAvg.AutoSize = true;
            lblAdaAvg.Location = new Point(911, 541);
            lblAdaAvg.Name = "lblAdaAvg";
            lblAdaAvg.Size = new Size(62, 15);
            lblAdaAvg.TabIndex = 1;
            lblAdaAvg.Text = "lblAdaAvg";
            // 
            // lblCashLocked
            // 
            lblCashLocked.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblCashLocked.AutoSize = true;
            lblCashLocked.Location = new Point(3, 80);
            lblCashLocked.Name = "lblCashLocked";
            lblCashLocked.Size = new Size(84, 15);
            lblCashLocked.TabIndex = 1;
            lblCashLocked.Text = "lblCashLocked";
            // 
            // lblCashAvailable
            // 
            lblCashAvailable.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblCashAvailable.AutoSize = true;
            lblCashAvailable.Location = new Point(3, 49);
            lblCashAvailable.Name = "lblCashAvailable";
            lblCashAvailable.Size = new Size(94, 15);
            lblCashAvailable.TabIndex = 1;
            lblCashAvailable.Text = "lblCashAvailable";
            // 
            // lblAdaProfitRate
            // 
            lblAdaProfitRate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaProfitRate.AutoSize = true;
            lblAdaProfitRate.Location = new Point(7, 32);
            lblAdaProfitRate.Name = "lblAdaProfitRate";
            lblAdaProfitRate.Size = new Size(93, 15);
            lblAdaProfitRate.TabIndex = 1;
            lblAdaProfitRate.Text = "lblAdaProfitRate";
            // 
            // lblAdaProfitLoss
            // 
            lblAdaProfitLoss.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaProfitLoss.AutoSize = true;
            lblAdaProfitLoss.Location = new Point(7, 63);
            lblAdaProfitLoss.Name = "lblAdaProfitLoss";
            lblAdaProfitLoss.Size = new Size(93, 15);
            lblAdaProfitLoss.TabIndex = 1;
            lblAdaProfitLoss.Text = "lblAdaProfitLoss";
            // 
            // lblTotalEval
            // 
            lblTotalEval.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblTotalEval.AutoSize = true;
            lblTotalEval.Location = new Point(911, 702);
            lblTotalEval.Name = "lblTotalEval";
            lblTotalEval.Size = new Size(67, 15);
            lblTotalEval.TabIndex = 1;
            lblTotalEval.Text = "lblTotalEval";
            // 
            // lblTotalBuy
            // 
            lblTotalBuy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblTotalBuy.AutoSize = true;
            lblTotalBuy.Location = new Point(911, 678);
            lblTotalBuy.Name = "lblTotalBuy";
            lblTotalBuy.Size = new Size(66, 15);
            lblTotalBuy.TabIndex = 1;
            lblTotalBuy.Text = "lblTotalBuy";
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel1, toolStripStatusLabel2 });
            statusStrip1.Location = new Point(0, 853);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1097, 22);
            statusStrip1.TabIndex = 2;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(121, 17);
            toolStripStatusLabel1.Text = "toolStripStatusLabel1";
            toolStripStatusLabel1.Click += toolStripStatusLabel1_Click;
            toolStripStatusLabel1.DoubleClick += toolStripStatusLabel1_DoubleClick;
            // 
            // toolStripStatusLabel2
            // 
            toolStripStatusLabel2.AutoSize = false;
            toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            toolStripStatusLabel2.Size = new Size(121, 17);
            toolStripStatusLabel2.Text = "toolStripStatusLabel2";
            toolStripStatusLabel2.TextAlign = ContentAlignment.MiddleRight;
            // 
            // textBox1
            // 
            textBox1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox1.Location = new Point(12, 814);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ScrollBars = ScrollBars.Vertical;
            textBox1.Size = new Size(1073, 36);
            textBox1.TabIndex = 3;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            groupBox1.Controls.Add(lblCashTotal);
            groupBox1.Controls.Add(lblCashAvailable);
            groupBox1.Controls.Add(lblCashLocked);
            groupBox1.Location = new Point(905, 320);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(179, 100);
            groupBox1.TabIndex = 4;
            groupBox1.TabStop = false;
            groupBox1.Text = "groupBox1";
            // 
            // groupBox2
            // 
            groupBox2.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            groupBox2.Controls.Add(lblAdaProfitRate);
            groupBox2.Controls.Add(lblAdaProfitLoss);
            groupBox2.Location = new Point(905, 575);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(180, 100);
            groupBox2.TabIndex = 2;
            groupBox2.TabStop = false;
            groupBox2.Text = "groupBox2";
            // 
            // dataGridView1
            // 
            dataGridView1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new Point(12, 725);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new Size(1073, 83);
            dataGridView1.TabIndex = 5;
            dataGridView1.CellClick += dataGridView1_CellClick;
            // 
            // checkedListBox_ChartSeries
            // 
            checkedListBox_ChartSeries.Dock = DockStyle.Top;
            checkedListBox_ChartSeries.FormattingEnabled = true;
            checkedListBox_ChartSeries.Location = new Point(3, 3);
            checkedListBox_ChartSeries.Name = "checkedListBox_ChartSeries";
            checkedListBox_ChartSeries.Size = new Size(186, 166);
            checkedListBox_ChartSeries.TabIndex = 9;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { toolStripMenuItem2, flowToolStripMenuItem, tsHeatmap });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1097, 24);
            menuStrip1.TabIndex = 10;
            menuStrip1.Text = "menuStrip1";
            // 
            // toolStripMenuItem2
            // 
            toolStripMenuItem2.Name = "toolStripMenuItem2";
            toolStripMenuItem2.Size = new Size(60, 20);
            toolStripMenuItem2.Text = "API Key";
            toolStripMenuItem2.Click += toolStripMenuItem2_Click;
            // 
            // flowToolStripMenuItem
            // 
            flowToolStripMenuItem.Name = "flowToolStripMenuItem";
            flowToolStripMenuItem.Size = new Size(44, 20);
            flowToolStripMenuItem.Text = "Flow";
            flowToolStripMenuItem.Click += flowToolStripMenuItem_Click;
            // 
            // txtSimulateCash
            // 
            txtSimulateCash.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtSimulateCash.Location = new Point(7, 47);
            txtSimulateCash.Name = "txtSimulateCash";
            txtSimulateCash.Size = new Size(116, 23);
            txtSimulateCash.TabIndex = 11;
            // 
            // btnApplySimulation
            // 
            btnApplySimulation.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnApplySimulation.Location = new Point(129, 47);
            btnApplySimulation.Name = "btnApplySimulation";
            btnApplySimulation.Size = new Size(45, 23);
            btnApplySimulation.TabIndex = 13;
            btnApplySimulation.Text = "실행";
            btnApplySimulation.UseVisualStyleBackColor = true;
            btnApplySimulation.Click += btnApplySimulation_Click;
            // 
            // chkShowSimulateLine
            // 
            chkShowSimulateLine.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkShowSimulateLine.AutoSize = true;
            chkShowSimulateLine.Location = new Point(7, 22);
            chkShowSimulateLine.Name = "chkShowSimulateLine";
            chkShowSimulateLine.Size = new Size(90, 19);
            chkShowSimulateLine.TabIndex = 14;
            chkShowSimulateLine.Text = "예상 평단가";
            chkShowSimulateLine.UseVisualStyleBackColor = true;
            // 
            // lblExpectedAvg
            // 
            lblExpectedAvg.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblExpectedAvg.AutoSize = true;
            lblExpectedAvg.Location = new Point(102, 24);
            lblExpectedAvg.Name = "lblExpectedAvg";
            lblExpectedAvg.Size = new Size(32, 15);
            lblExpectedAvg.TabIndex = 15;
            lblExpectedAvg.Text = "KRW";
            // 
            // tabControl1
            // 
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Location = new Point(886, 27);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(200, 287);
            tabControl1.TabIndex = 16;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(groupBox3);
            tabPage1.Controls.Add(checkedListBox_ChartSeries);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(192, 259);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "시리즈 목록";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            groupBox3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            groupBox3.Controls.Add(chkShowSimulateLine);
            groupBox3.Controls.Add(btnApplySimulation);
            groupBox3.Controls.Add(lblExpectedAvg);
            groupBox3.Controls.Add(txtSimulateCash);
            groupBox3.Location = new Point(6, 175);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(180, 81);
            groupBox3.TabIndex = 16;
            groupBox3.TabStop = false;
            groupBox3.Text = "부가기능";
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(grpVolumeAlarm);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(192, 259);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Alram";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // grpVolumeAlarm
            // 
            grpVolumeAlarm.Controls.Add(lblCooldownValue);
            grpVolumeAlarm.Controls.Add(label3);
            grpVolumeAlarm.Controls.Add(label2);
            grpVolumeAlarm.Controls.Add(label1);
            grpVolumeAlarm.Controls.Add(trkbCooldown);
            grpVolumeAlarm.Controls.Add(cmbAlarmSound);
            grpVolumeAlarm.Controls.Add(lblTargetVol);
            grpVolumeAlarm.Controls.Add(lblCurrentAvg);
            grpVolumeAlarm.Controls.Add(numVolMultiplier);
            grpVolumeAlarm.Controls.Add(chkAlarmEnable);
            grpVolumeAlarm.Dock = DockStyle.Fill;
            grpVolumeAlarm.Location = new Point(3, 3);
            grpVolumeAlarm.Name = "grpVolumeAlarm";
            grpVolumeAlarm.Size = new Size(186, 253);
            grpVolumeAlarm.TabIndex = 0;
            grpVolumeAlarm.TabStop = false;
            grpVolumeAlarm.Text = "실시간 거래량 알람";
            // 
            // lblCooldownValue
            // 
            lblCooldownValue.AutoSize = true;
            lblCooldownValue.Location = new Point(27, 192);
            lblCooldownValue.Name = "lblCooldownValue";
            lblCooldownValue.Size = new Size(39, 15);
            lblCooldownValue.TabIndex = 9;
            lblCooldownValue.Text = "label4";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(6, 87);
            label3.Name = "label3";
            label3.Size = new Size(62, 15);
            label3.TabIndex = 8;
            label3.Text = "배수 설정:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(6, 59);
            label2.Name = "label2";
            label2.Size = new Size(62, 15);
            label2.TabIndex = 7;
            label2.Text = "알람 종류:";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(6, 171);
            label1.Name = "label1";
            label1.Size = new Size(74, 15);
            label1.TabIndex = 6;
            label1.Text = "재알람 금지:";
            // 
            // trkbCooldown
            // 
            trkbCooldown.Location = new Point(86, 171);
            trkbCooldown.Maximum = 300;
            trkbCooldown.Minimum = 10;
            trkbCooldown.Name = "trkbCooldown";
            trkbCooldown.Size = new Size(87, 45);
            trkbCooldown.TabIndex = 5;
            trkbCooldown.Value = 60;
            // 
            // cmbAlarmSound
            // 
            cmbAlarmSound.FormattingEnabled = true;
            cmbAlarmSound.Items.AddRange(new object[] { "Beep", "Siren", "Bell" });
            cmbAlarmSound.Location = new Point(74, 56);
            cmbAlarmSound.Name = "cmbAlarmSound";
            cmbAlarmSound.Size = new Size(100, 23);
            cmbAlarmSound.TabIndex = 4;
            // 
            // lblTargetVol
            // 
            lblTargetVol.AutoSize = true;
            lblTargetVol.ForeColor = Color.Orange;
            lblTargetVol.Location = new Point(6, 138);
            lblTargetVol.Name = "lblTargetVol";
            lblTargetVol.Size = new Size(83, 15);
            lblTargetVol.TabIndex = 3;
            lblTargetVol.Text = "알람 기준량: -";
            // 
            // lblCurrentAvg
            // 
            lblCurrentAvg.AutoSize = true;
            lblCurrentAvg.ForeColor = SystemColors.GrayText;
            lblCurrentAvg.Location = new Point(6, 117);
            lblCurrentAvg.Name = "lblCurrentAvg";
            lblCurrentAvg.Size = new Size(101, 15);
            lblCurrentAvg.TabIndex = 2;
            lblCurrentAvg.Text = "현재 20분 평균: -";
            // 
            // numVolMultiplier
            // 
            numVolMultiplier.DecimalPlaces = 1;
            numVolMultiplier.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
            numVolMultiplier.Location = new Point(74, 85);
            numVolMultiplier.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            numVolMultiplier.Minimum = new decimal(new int[] { 10, 0, 0, 65536 });
            numVolMultiplier.Name = "numVolMultiplier";
            numVolMultiplier.Size = new Size(100, 23);
            numVolMultiplier.TabIndex = 1;
            numVolMultiplier.TextAlign = HorizontalAlignment.Right;
            numVolMultiplier.Value = new decimal(new int[] { 50, 0, 0, 65536 });
            // 
            // chkAlarmEnable
            // 
            chkAlarmEnable.AutoSize = true;
            chkAlarmEnable.Checked = true;
            chkAlarmEnable.CheckState = CheckState.Checked;
            chkAlarmEnable.Location = new Point(6, 22);
            chkAlarmEnable.Name = "chkAlarmEnable";
            chkAlarmEnable.Size = new Size(90, 19);
            chkAlarmEnable.TabIndex = 0;
            chkAlarmEnable.Text = "알람 활성화";
            chkAlarmEnable.UseVisualStyleBackColor = true;
            // 
            // tsHeatmap
            // 
            tsHeatmap.Name = "tsHeatmap";
            tsHeatmap.Size = new Size(68, 20);
            tsHeatmap.Text = "Heetmap";
            tsHeatmap.Click += tsHeatmap_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1097, 875);
            Controls.Add(tabControl1);
            Controls.Add(dataGridView1);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(textBox1);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            Controls.Add(lblTotalBuy);
            Controls.Add(lblTotalEval);
            Controls.Add(lblAdaAvg);
            Controls.Add(lblAdaInventory);
            Controls.Add(lblAdaLocked);
            Controls.Add(lblAdaBalance);
            Controls.Add(formsPlot1);
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            tabPage2.ResumeLayout(false);
            grpVolumeAlarm.ResumeLayout(false);
            grpVolumeAlarm.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trkbCooldown).EndInit();
            ((System.ComponentModel.ISupportInitialize)numVolMultiplier).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ScottPlot.WinForms.FormsPlot formsPlot1;
        private Label lblCashTotal;
        private Label lblAdaBalance;
        private Label lblAdaLocked;
        private Label lblAdaInventory;
        private Label lblAdaAvg;
        private Label lblCashLocked;
        private Label lblCashAvailable;
        private Label lblAdaProfitRate;
        private Label lblAdaProfitLoss;
        private Label lblTotalEval;
        private Label lblTotalBuy;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private TextBox textBox1;
        private GroupBox groupBox1;
        private GroupBox groupBox2;
        private DataGridView dataGridView1;
        private ToolStripStatusLabel toolStripStatusLabel2;
        private CheckedListBox checkedListBox_ChartSeries;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem toolStripMenuItem2;
        private ToolStripMenuItem flowToolStripMenuItem;
        private TextBox txtSimulateCash;
        private Button btnApplySimulation;
        private CheckBox chkShowSimulateLine;
        private Label lblExpectedAvg;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private GroupBox grpVolumeAlarm;
        private NumericUpDown numVolMultiplier;
        private CheckBox chkAlarmEnable;
        private ComboBox cmbAlarmSound;
        private Label lblTargetVol;
        private Label lblCurrentAvg;
        private Label label1;
        private TrackBar trkbCooldown;
        private Label label3;
        private Label label2;
        private GroupBox groupBox3;
        private Label lblCooldownValue;
        private ToolStripMenuItem tsHeatmap;
    }
}
