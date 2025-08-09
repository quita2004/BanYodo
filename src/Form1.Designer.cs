namespace BanYodo.Forms;

partial class MainForm
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
        buttonPanel = new Panel();
        addAccountButton = new Button();
        clearAllButton = new Button();
        stopAllButton = new Button();
        startAllButton = new Button();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        websitePanel = new Panel();
        websiteComboBox = new ComboBox();
        websiteLabel = new Label();
        mainTableLayoutPanel = new TableLayoutPanel();
        buttonPanel.SuspendLayout();
        statusStrip.SuspendLayout();
        websitePanel.SuspendLayout();
        mainTableLayoutPanel.SuspendLayout();
        SuspendLayout();
        // 
        // buttonPanel
        // 
        buttonPanel.Controls.Add(addAccountButton);
        buttonPanel.Controls.Add(clearAllButton);
        buttonPanel.Controls.Add(stopAllButton);
        buttonPanel.Controls.Add(startAllButton);
        buttonPanel.Dock = DockStyle.Bottom;
        buttonPanel.Location = new Point(0, 658);
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Size = new Size(1200, 60);
        buttonPanel.TabIndex = 1;
        // 
        // addAccountButton
        // 
        addAccountButton.BackColor = Color.FromArgb(46, 204, 113);
        addAccountButton.Cursor = Cursors.Hand;
        addAccountButton.FlatAppearance.BorderSize = 0;
        addAccountButton.FlatStyle = FlatStyle.Flat;
        addAccountButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        addAccountButton.ForeColor = Color.White;
        addAccountButton.Location = new Point(12, 15);
        addAccountButton.Name = "addAccountButton";
        addAccountButton.Size = new Size(100, 30);
        addAccountButton.TabIndex = 2;
        addAccountButton.Text = "Add Account";
        addAccountButton.UseVisualStyleBackColor = false;
        addAccountButton.Click += AddAccountButton_Click;
        // 
        // clearAllButton
        // 
        clearAllButton.BackColor = Color.FromArgb(155, 89, 182);
        clearAllButton.Cursor = Cursors.Hand;
        clearAllButton.FlatAppearance.BorderSize = 0;
        clearAllButton.FlatStyle = FlatStyle.Flat;
        clearAllButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        clearAllButton.ForeColor = Color.White;
        clearAllButton.Location = new Point(118, 15);
        clearAllButton.Name = "clearAllButton";
        clearAllButton.Size = new Size(100, 30);
        clearAllButton.TabIndex = 4;
        clearAllButton.Text = "Clear All";
        clearAllButton.UseVisualStyleBackColor = false;
        clearAllButton.Click += ClearAllButton_Click;
        // 
        // stopAllButton
        // 
        stopAllButton.BackColor = Color.FromArgb(241, 196, 15);
        stopAllButton.Cursor = Cursors.Hand;
        stopAllButton.FlatAppearance.BorderSize = 0;
        stopAllButton.FlatStyle = FlatStyle.Flat;
        stopAllButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        stopAllButton.ForeColor = Color.White;
        stopAllButton.Location = new Point(922, 15);
        stopAllButton.Name = "stopAllButton";
        stopAllButton.Size = new Size(100, 30);
        stopAllButton.TabIndex = 1;
        stopAllButton.Text = "Stop All";
        stopAllButton.UseVisualStyleBackColor = false;
        stopAllButton.Click += StopAllButton_Click;
        // 
        // startAllButton
        // 
        startAllButton.BackColor = Color.FromArgb(52, 152, 219);
        startAllButton.Cursor = Cursors.Hand;
        startAllButton.FlatAppearance.BorderSize = 0;
        startAllButton.FlatStyle = FlatStyle.Flat;
        startAllButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        startAllButton.ForeColor = Color.White;
        startAllButton.Location = new Point(1088, 15);
        startAllButton.Name = "startAllButton";
        startAllButton.Size = new Size(100, 30);
        startAllButton.TabIndex = 0;
        startAllButton.Text = "Start All";
        startAllButton.UseVisualStyleBackColor = false;
        startAllButton.Click += StartAllButton_Click;
        // 
        // statusStrip
        // 
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
        statusStrip.Location = new Point(0, 718);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(1200, 22);
        statusStrip.TabIndex = 2;
        statusStrip.Text = "statusStrip1";
        statusStrip.ItemClicked += statusStrip_ItemClicked;
        // 
        // statusLabel
        // 
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(39, 17);
        statusLabel.Text = "Ready";
        // 
        // websitePanel
        // 
        mainTableLayoutPanel.SetColumnSpan(websitePanel, 2);
        websitePanel.Controls.Add(websiteComboBox);
        websitePanel.Controls.Add(websiteLabel);
        websitePanel.Dock = DockStyle.Fill;
        websitePanel.Location = new Point(3, 3);
        websitePanel.Name = "websitePanel";
        websitePanel.Size = new Size(1194, 44);
        websitePanel.TabIndex = 0;
        // 
        // websiteComboBox
        // 
        websiteComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        websiteComboBox.FormattingEnabled = true;
        websiteComboBox.Items.AddRange(new object[] { "Yodobashi.com", "Fujifilm.com (Coming Soon)" });
        websiteComboBox.Location = new Point(80, 12);
        websiteComboBox.Name = "websiteComboBox";
        websiteComboBox.Size = new Size(150, 23);
        websiteComboBox.TabIndex = 1;
        websiteComboBox.SelectedIndexChanged += WebsiteComboBox_SelectedIndexChanged;
        // 
        // websiteLabel
        // 
        websiteLabel.AutoSize = true;
        websiteLabel.Location = new Point(10, 15);
        websiteLabel.Name = "websiteLabel";
        websiteLabel.Size = new Size(52, 15);
        websiteLabel.TabIndex = 0;
        websiteLabel.Text = "Website:";
        // 
        // mainTableLayoutPanel
        // 
        mainTableLayoutPanel.ColumnCount = 2;
        mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        mainTableLayoutPanel.Controls.Add(websitePanel, 0, 0);
        mainTableLayoutPanel.Dock = DockStyle.Fill;
        mainTableLayoutPanel.Location = new Point(0, 0);
        mainTableLayoutPanel.Name = "mainTableLayoutPanel";
        mainTableLayoutPanel.RowCount = 3;
        mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 230F));
        mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainTableLayoutPanel.Size = new Size(1200, 658);
        mainTableLayoutPanel.TabIndex = 0;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1200, 740);
        Controls.Add(mainTableLayoutPanel);
        Controls.Add(buttonPanel);
        Controls.Add(statusStrip);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "BanYodo - Auto Purchase Application";
        FormClosing += MainForm_FormClosing;
        Load += MainForm_Load;
        buttonPanel.ResumeLayout(false);
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        websitePanel.ResumeLayout(false);
        websitePanel.PerformLayout();
        mainTableLayoutPanel.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
    private System.Windows.Forms.Panel buttonPanel;
    private System.Windows.Forms.Button startAllButton;
    private System.Windows.Forms.Button stopAllButton;
    private System.Windows.Forms.Button addAccountButton;
    private System.Windows.Forms.Button clearAllButton;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel statusLabel;
    private Panel websitePanel;
    private TableLayoutPanel mainTableLayoutPanel;
    private ComboBox websiteComboBox;
    private Label websiteLabel;
}
