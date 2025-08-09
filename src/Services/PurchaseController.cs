using BanYodo.Models;
using BanYodo.Strategies;

namespace BanYodo.Services
{
    public class PurchaseController
    {
        private readonly PuppeteerService _puppeteerService;
        private readonly Dictionary<Website, IPurchaseStrategy> _strategies;
        private readonly Dictionary<string, CancellationTokenSource> _runningTasks;
        private readonly Dictionary<string, System.Threading.Timer> _scanTimers;

        public event EventHandler<AccountStatusChangedEventArgs> AccountStatusChanged;

        public PurchaseController(PuppeteerService puppeteerService)
        {
            _puppeteerService = puppeteerService;
            _strategies = new Dictionary<Website, IPurchaseStrategy>
            {
                { Website.Yodobashi, new YodobashiPurchaseStrategy() },
                { Website.Fujifilm, new FujifilmPurchaseStrategy() }
            };
            _runningTasks = new Dictionary<string, CancellationTokenSource>();
            _scanTimers = new Dictionary<string, System.Threading.Timer>();
        }

        public async Task StartAccountAsync(Account account, Configuration config, int scanIntervalSeconds = 5)
        {
            if (account.IsRunning)
                return;

            account.IsRunning = true;
            account.Status = AccountStatus.Running;
            OnAccountStatusChanged(account);

            var accountKey = GetAccountKey(account);
            var cancellationTokenSource = new CancellationTokenSource();
            _runningTasks[accountKey] = cancellationTokenSource;

            try
            {
                // Launch browser for this account
                var accountId = await _puppeteerService.LaunchBrowserForAccountAsync(account);
                await _puppeteerService.NavigateToWebsiteAsync(accountId, config.SelectedWebsite);

                if (config.PurchaseMode == PurchaseMode.FixedTime)
                {
                    await StartFixedTimeModeAsync(account, config, accountId, cancellationTokenSource.Token);
                }
                else
                {
                    await StartScanModeAsync(account, config, accountId, scanIntervalSeconds, cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                account.Status = AccountStatus.Failed;
                account.IsRunning = false;
                OnAccountStatusChanged(account);
                // Log error
            }
        }

        public async Task StopAccountAsync(Account account)
        {
            var accountKey = GetAccountKey(account);
            
            if (_runningTasks.ContainsKey(accountKey))
            {
                _runningTasks[accountKey].Cancel();
                _runningTasks.Remove(accountKey);
            }

            if (_scanTimers.ContainsKey(accountKey))
            {
                _scanTimers[accountKey].Dispose();
                _scanTimers.Remove(accountKey);
            }

            account.IsRunning = false;
            account.Status = AccountStatus.Stopped;
            OnAccountStatusChanged(account);

            // Close browser for this account
            var accountId = $"{account.Username}_{Guid.NewGuid():N}"; // This should be stored properly
            await _puppeteerService.CloseBrowserAsync(accountId);
        }

        public async Task StartAllAccountsAsync(List<Account> accounts, Configuration config, int scanIntervalSeconds = 5)
        {
            var tasks = accounts.Where(a => !a.IsRunning).Select(account => StartAccountAsync(account, config, scanIntervalSeconds));
            await Task.WhenAll(tasks);
        }

        public async Task StopAllAccountsAsync(List<Account> accounts)
        {
            var tasks = accounts.Where(a => a.IsRunning).Select(StopAccountAsync);
            await Task.WhenAll(tasks);
        }

        private async Task StartFixedTimeModeAsync(Account account, Configuration config, string accountId, CancellationToken cancellationToken)
        {
            try
            {
                if (config.FixedTime.HasValue)
                {
                    var delay = config.FixedTime.Value - DateTime.Now;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    await ExecutePurchaseAsync(account, config, accountId);
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled
            }
        }

        private async Task StartScanModeAsync(Account account, Configuration config, string accountId, int scanIntervalSeconds, CancellationToken cancellationToken)
        {
            var accountKey = GetAccountKey(account);
            
            var timer = new System.Threading.Timer(async _ => 
            {
                if (!cancellationToken.IsCancellationRequested && account.IsRunning)
                {
                    await ExecutePurchaseAsync(account, config, accountId);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(scanIntervalSeconds));

            _scanTimers[accountKey] = timer;

            try
            {
                // Keep the task alive until cancelled
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                timer?.Dispose();
            }
        }

        private async Task ExecutePurchaseAsync(Account account, Configuration config, string accountId)
        {
            try
            {
                var strategy = _strategies[config.SelectedWebsite];
                var page = _puppeteerService.GetPageForAccount(accountId);
                
                if (page == null)
                    return;

                // Login if not already logged in
                if (!await strategy.IsLoggedInAsync(page))
                {
                    if (!await strategy.LoginAsync(page, account))
                    {
                        account.Status = AccountStatus.Failed;
                        OnAccountStatusChanged(account);
                        return;
                    }
                }

                // Try to purchase each product
                foreach (var productId in config.ProductIds)
                {
                    if (await strategy.CheckProductAvailabilityAsync(page, productId))
                    {
                        if (await strategy.PurchaseProductAsync(page, productId, account))
                        {
                            account.Status = AccountStatus.Success;
                            OnAccountStatusChanged(account);
                            
                            // Stop this account after successful purchase
                            await StopAccountAsync(account);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                account.Status = AccountStatus.Failed;
                OnAccountStatusChanged(account);
                // Log error
            }
        }

        private string GetAccountKey(Account account)
        {
            return $"{account.Username}_{account.GetHashCode()}";
        }

        private void OnAccountStatusChanged(Account account)
        {
            AccountStatusChanged?.Invoke(this, new AccountStatusChangedEventArgs(account));
        }

        public void Dispose()
        {
            foreach (var timer in _scanTimers.Values)
            {
                timer?.Dispose();
            }
            
            foreach (var cts in _runningTasks.Values)
            {
                cts?.Cancel();
            }
            
            _scanTimers.Clear();
            _runningTasks.Clear();
        }
    }

    public class AccountStatusChangedEventArgs : EventArgs
    {
        public Account Account { get; }

        public AccountStatusChangedEventArgs(Account account)
        {
            Account = account;
        }
    }
}
