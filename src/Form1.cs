using BanYodo.Models;
using BanYodo.Services;
using BanYodo.Forms.Controls;

namespace BanYodo.Forms;

public partial class MainForm : Form
{
    private readonly ConfigurationService _configurationService;
    private readonly PuppeteerService _puppeteerService;
    private readonly PurchaseController _purchaseController;
    private readonly LoggingService _loggingService;

    private Configuration _currentConfiguration;
    private AccountDataGridView _accountDataGridView;
    private PurchaseModePanel _purchaseModePanel;
    private ProductIdsPanel _productIdsPanel;

    public MainForm()
    {
        InitializeComponent();

        _configurationService = new ConfigurationService();
        _puppeteerService = new PuppeteerService();
        _purchaseController = new PurchaseController(_puppeteerService);
        _loggingService = new LoggingService();

        _currentConfiguration = new Configuration();

        InitializeCustomComponents();
        SetupEventHandlers();

        _loggingService.LogInfo("Application started");
    }

    private void InitializeCustomComponents()
    {
        // Initialize custom controls
        _accountDataGridView = new AccountDataGridView();
        _purchaseModePanel = new PurchaseModePanel();
        _productIdsPanel = new ProductIdsPanel();

        // Add custom controls to the layout
        AddCustomControlsToLayout();
    }

    private void AddCustomControlsToLayout()
    {
        // Add Product IDs panel to row 1, column 0
        _productIdsPanel.Dock = DockStyle.Fill;
        mainTableLayoutPanel.Controls.Add(_productIdsPanel, 0, 1);

        // Add Purchase mode panel to row 1, column 1
        _purchaseModePanel.Dock = DockStyle.Fill;
        mainTableLayoutPanel.Controls.Add(_purchaseModePanel, 1, 1);

        // Add Account DataGridView to row 2, spanning both columns
        _accountDataGridView.Dock = DockStyle.Fill;
        mainTableLayoutPanel.Controls.Add(_accountDataGridView, 0, 2);
        mainTableLayoutPanel.SetColumnSpan(_accountDataGridView, 2);

        // Set initial ComboBox selection
        websiteComboBox.SelectedIndex = 0;
    }

    private void SetupEventHandlers()
    {
        _purchaseController.AccountStatusChanged += PurchaseController_AccountStatusChanged;
        this.Load += MainForm_Load;
        this.FormClosing += MainForm_FormClosing;
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        try
        {
            await _puppeteerService.InitializeBrowserAsync();
            await _configurationService.LoadConfigurationAsync();
            _currentConfiguration = _configurationService.GetConfiguration();

            UpdateUI();

            _loggingService.LogInfo("Configuration loaded successfully");
            statusLabel.Text = "Application loaded successfully";
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to load application", ex);
            MessageBox.Show($"Failed to initialize application: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Failed to load application";
        }
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        try
        {
            await _configurationService.SaveConfigurationAsync(_currentConfiguration);
            await _purchaseController.StopAllAccountsAsync(_currentConfiguration.Accounts);
            _purchaseController.Dispose();
            _configurationService.Dispose();
            _puppeteerService.Dispose();

            _loggingService.LogInfo("Application closed");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during application shutdown", ex);
        }
    }

    private void UpdateUI()
    {
        _accountDataGridView.LoadAccounts(_currentConfiguration.Accounts);
        _productIdsPanel.LoadProductIds(_currentConfiguration.ProductIds);
        _productIdsPanel.SetConfiguration(_currentConfiguration);
        _purchaseModePanel.LoadConfiguration(_currentConfiguration);

        // Setup event handlers for child controls
        _accountDataGridView.OnAccountActionClicked += AccountDataGridView_OnAccountActionClicked;
        _accountDataGridView.OnAccountChanged += AccountDataGridView_OnAccountChanged;
        _accountDataGridView.OnAccountRemoveClicked += AccountDataGridView_OnAccountRemoveClicked;
        _accountDataGridView.OnAllAccountsCleared += AccountDataGridView_OnAllAccountsCleared;
        _purchaseModePanel.OnConfigurationChanged += PurchaseModePanel_OnConfigurationChanged;
        _productIdsPanel.OnProductIdsChanged += ProductIdsPanel_OnProductIdsChanged;
    }

    private async void StartAllButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var scanInterval = _purchaseModePanel.GetScanInterval();
            await _purchaseController.StartAllAccountsAsync(_currentConfiguration.Accounts, _currentConfiguration, scanInterval);
            _loggingService.LogInfo("Started all accounts");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to start all accounts", ex);
            MessageBox.Show($"Failed to start accounts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void StopAllButton_Click(object? sender, EventArgs e)
    {
        try
        {
            await _purchaseController.StopAllAccountsAsync(_currentConfiguration.Accounts);
            _loggingService.LogInfo("Stopped all accounts");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to stop all accounts", ex);
            MessageBox.Show($"Failed to stop accounts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddAccountButton_Click(object? sender, EventArgs e)
    {
        var newAccount = new Account();
        _currentConfiguration.AddAccount(newAccount);
        _accountDataGridView.AddAccount(newAccount);

        _ = Task.Run(async () => await _configurationService.SaveConfigurationAsync(_currentConfiguration));
    }

    private void RemoveAccountButton_Click(object? sender, EventArgs e)
    {
        var selectedAccount = _accountDataGridView.GetSelectedAccount();
        if (selectedAccount != null)
        {
            _currentConfiguration.RemoveAccount(selectedAccount);
            _accountDataGridView.RemoveAccount(selectedAccount);

            _ = Task.Run(async () => await _configurationService.SaveConfigurationAsync(_currentConfiguration));
        }
    }

    private void ClearAllButton_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to clear all accounts?", "Confirm Clear All", 
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        
        if (result == DialogResult.Yes)
        {
            _currentConfiguration.ClearAccounts();
            _accountDataGridView.ClearAllAccounts();
        }
    }

    private void WebsiteComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var comboBox = sender as ComboBox;
        if (comboBox?.SelectedIndex == 0)
        {
            _currentConfiguration.SelectedWebsite = Website.Yodobashi;
        }
        else
        {
            MessageBox.Show("Fujifilm.com is not yet supported.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            comboBox!.SelectedIndex = 0;
        }
    }

    private void PurchaseController_AccountStatusChanged(object? sender, AccountStatusChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => PurchaseController_AccountStatusChanged(sender, e)));
            return;
        }

        _accountDataGridView.RefreshAccountStatus(e.Account);
        _loggingService.LogInfo($"Account {e.Account.Username} status changed to {e.Account.Status}");

        // Update status bar
        statusLabel.Text = $"Account {e.Account.Username}: {e.Account.Status}";
    }

    private async void AccountDataGridView_OnAccountActionClicked(Account account)
    {
        try
        {
            if (account.IsRunning)
            {
                await _purchaseController.StopAccountAsync(account);
            }
            else
            {
                var scanInterval = _purchaseModePanel.GetScanInterval();
                await _purchaseController.StartAccountAsync(account, _currentConfiguration, scanInterval);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to toggle account {account.Username}", ex);
            MessageBox.Show($"Failed to toggle account: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void PurchaseModePanel_OnConfigurationChanged()
    {
        await _configurationService.SaveConfigurationAsync(_currentConfiguration);
    }

    private async void ProductIdsPanel_OnProductIdsChanged()
    {
        await _configurationService.SaveConfigurationAsync(_currentConfiguration);
    }

    private async void AccountDataGridView_OnAccountChanged(Account account)
    {
        await _configurationService.SaveConfigurationAsync(_currentConfiguration);
    }

    private void AccountDataGridView_OnAccountRemoveClicked(Account account)
    {
        _currentConfiguration.RemoveAccount(account);
        _accountDataGridView.RemoveAccount(account);

        _ = Task.Run(async () => await _configurationService.SaveConfigurationAsync(_currentConfiguration));
    }

    private async void AccountDataGridView_OnAllAccountsCleared()
    {
        await _configurationService.SaveConfigurationAsync(_currentConfiguration);
    }

    private void statusStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
    {

    }
}
