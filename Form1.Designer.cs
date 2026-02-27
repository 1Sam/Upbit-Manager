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
            statusStrip1.SuspendLayout();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // formsPlot1
            // 
            formsPlot1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            formsPlot1.DisplayScale = 1F;
            formsPlot1.Font = new Font("맑은 고딕", 9F, FontStyle.Regular, GraphicsUnit.Point, 129);
            formsPlot1.Location = new Point(12, 27);
            formsPlot1.Name = "formsPlot1";
            formsPlot1.Size = new Size(872, 692);
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
            lblAdaBalance.Location = new Point(899, 329);
            lblAdaBalance.Name = "lblAdaBalance";
            lblAdaBalance.Size = new Size(82, 15);
            lblAdaBalance.TabIndex = 1;
            lblAdaBalance.Text = "lblAdaBalance";
            // 
            // lblAdaLocked
            // 
            lblAdaLocked.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaLocked.AutoSize = true;
            lblAdaLocked.Location = new Point(899, 357);
            lblAdaLocked.Name = "lblAdaLocked";
            lblAdaLocked.Size = new Size(79, 15);
            lblAdaLocked.TabIndex = 1;
            lblAdaLocked.Text = "lblAdaLocked";
            // 
            // lblAdaInventory
            // 
            lblAdaInventory.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaInventory.AutoSize = true;
            lblAdaInventory.Location = new Point(899, 390);
            lblAdaInventory.Name = "lblAdaInventory";
            lblAdaInventory.Size = new Size(91, 15);
            lblAdaInventory.TabIndex = 1;
            lblAdaInventory.Text = "lblAdaInventory";
            // 
            // lblAdaAvg
            // 
            lblAdaAvg.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaAvg.AutoSize = true;
            lblAdaAvg.Location = new Point(899, 422);
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
            lblAdaProfitRate.Location = new Point(-13, 19);
            lblAdaProfitRate.Name = "lblAdaProfitRate";
            lblAdaProfitRate.Size = new Size(93, 15);
            lblAdaProfitRate.TabIndex = 1;
            lblAdaProfitRate.Text = "lblAdaProfitRate";
            // 
            // lblAdaProfitLoss
            // 
            lblAdaProfitLoss.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblAdaProfitLoss.AutoSize = true;
            lblAdaProfitLoss.Location = new Point(-13, 50);
            lblAdaProfitLoss.Name = "lblAdaProfitLoss";
            lblAdaProfitLoss.Size = new Size(93, 15);
            lblAdaProfitLoss.TabIndex = 1;
            lblAdaProfitLoss.Text = "lblAdaProfitLoss";
            // 
            // lblTotalEval
            // 
            lblTotalEval.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblTotalEval.AutoSize = true;
            lblTotalEval.Location = new Point(899, 583);
            lblTotalEval.Name = "lblTotalEval";
            lblTotalEval.Size = new Size(67, 15);
            lblTotalEval.TabIndex = 1;
            lblTotalEval.Text = "lblTotalEval";
            // 
            // lblTotalBuy
            // 
            lblTotalBuy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblTotalBuy.AutoSize = true;
            lblTotalBuy.Location = new Point(899, 559);
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
            statusStrip1.Size = new Size(1081, 22);
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
            textBox1.Size = new Size(1057, 36);
            textBox1.TabIndex = 3;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            groupBox1.Controls.Add(lblCashTotal);
            groupBox1.Controls.Add(lblCashAvailable);
            groupBox1.Controls.Add(lblCashLocked);
            groupBox1.Location = new Point(893, 215);
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
            groupBox2.Location = new Point(893, 456);
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
            dataGridView1.Size = new Size(1057, 83);
            dataGridView1.TabIndex = 5;
            dataGridView1.CellClick += dataGridView1_CellClick;
            // 
            // checkedListBox_ChartSeries
            // 
            checkedListBox_ChartSeries.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            checkedListBox_ChartSeries.FormattingEnabled = true;
            checkedListBox_ChartSeries.Location = new Point(890, 43);
            checkedListBox_ChartSeries.Name = "checkedListBox_ChartSeries";
            checkedListBox_ChartSeries.Size = new Size(183, 166);
            checkedListBox_ChartSeries.TabIndex = 9;
            checkedListBox_ChartSeries.SelectedIndexChanged += checkedListBox_ChartSeries_SelectedIndexChanged;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { toolStripMenuItem2, flowToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1081, 24);
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
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1081, 875);
            Controls.Add(checkedListBox_ChartSeries);
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
    }
}
