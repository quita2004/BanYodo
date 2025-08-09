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
        removeAccountButton = new Button();
        addAccountButton = new Button();
        stopAllButton = new Button();
        startAllButton = new Button();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        websitePanel = new Panel();
        websiteLabel = new Label();
        websiteComboBox = new ComboBox();
        mainTableLayoutPanel = new TableLayoutPanel();
        buttonPanel.SuspendLayout();
        statusStrip.SuspendLayout();
        websitePanel.SuspendLayout();
        mainTableLayoutPanel.SuspendLayout();
        SuspendLayout();
        // 
        // buttonPanel
        // 
        buttonPanel.Controls.Add(removeAccountButton);
        buttonPanel.Controls.Add(addAccountButton);
        buttonPanel.Controls.Add(stopAllButton);
        buttonPanel.Controls.Add(startAllButton);
        buttonPanel.Dock = DockStyle.Bottom;
        buttonPanel.Location = new Point(0, 658);
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Size = new Size(1200, 60);
        buttonPanel.TabIndex = 1;
        // 
        // removeAccountButton
        // 
        removeAccountButton.Location = new Point(340, 15);
        removeAccountButton.Name = "removeAccountButton";
        removeAccountButton.Size = new Size(120, 30);
        removeAccountButton.TabIndex = 3;
        removeAccountButton.Text = "Remove Account";
        removeAccountButton.UseVisualStyleBackColor = true;
        removeAccountButton.Click += RemoveAccountButton_Click;
        // 
        // addAccountButton
        // 
        addAccountButton.Location = new Point(230, 15);
        addAccountButton.Name = "addAccountButton";
        addAccountButton.Size = new Size(100, 30);
        addAccountButton.TabIndex = 2;
        addAccountButton.Text = "Add Account";
        addAccountButton.UseVisualStyleBackColor = true;
        addAccountButton.Click += AddAccountButton_Click;
        // 
        // stopAllButton
        // 
        stopAllButton.Location = new Point(120, 15);
        stopAllButton.Name = "stopAllButton";
        stopAllButton.Size = new Size(100, 30);
        stopAllButton.TabIndex = 1;
        stopAllButton.Text = "Stop All";
        stopAllButton.UseVisualStyleBackColor = true;
        stopAllButton.Click += StopAllButton_Click;
        // 
        // startAllButton
        // 
        startAllButton.Location = new Point(10, 15);
        startAllButton.Name = "startAllButton";
        startAllButton.Size = new Size(100, 30);
        startAllButton.TabIndex = 0;
        startAllButton.Text = "Start All";
        startAllButton.UseVisualStyleBackColor = true;
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
        // websiteLabel
        // 
        websiteLabel.AutoSize = true;
        websiteLabel.Location = new Point(10, 15);
        websiteLabel.Name = "websiteLabel";
        websiteLabel.Size = new Size(52, 15);
        websiteLabel.TabIndex = 0;
        websiteLabel.Text = "Website:";
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
        mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 280F));
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
    private System.Windows.Forms.Button removeAccountButton;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel statusLabel;
    private Panel websitePanel;
    private TableLayoutPanel mainTableLayoutPanel;
    private ComboBox websiteComboBox;
    private Label websiteLabel;
}
