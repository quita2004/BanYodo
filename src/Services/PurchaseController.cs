using BanYodo.Models;
using BanYodo.Strategies;
using PuppeteerSharp;
using System.Security.Principal;
using System.Windows.Forms;

namespace BanYodo.Services
{
    public class PurchaseController
    {
        private readonly PuppeteerService _puppeteerService;
        private readonly LoggingService _loggingService;
        private readonly Dictionary<Website, IPurchaseStrategy> _strategies;
        private readonly Dictionary<string, CancellationTokenSource> _runningTasks;
        private readonly Dictionary<string, System.Threading.Timer> _scanTimers;
        private readonly int timeRunBeforeSecond = 300;

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
            _loggingService = new LoggingService();
        }

        public async Task StartAccountAsync(Account account, Configuration config, int scanIntervalSeconds = 5)
        {
            if (account.IsRunning)
                return;

            account.IsRunning = true;
            account.Status = AccountStatus.Running;
            account.StatusText = "Bắt đầu...";
            OnAccountStatusChanged(account);

            var accountKey = GetAccountKey(account);
            var cancellationTokenSource = new CancellationTokenSource();
            _runningTasks[accountKey] = cancellationTokenSource;

            try
            {
                // Launch browser for this account
                await ExecutePurchaseAsync(account, config);
            }
            catch (Exception ex)
            {
                account.Status = AccountStatus.Failed;
                account.IsRunning = false;
                OnAccountStatusChanged(account);
                // Log error
                _loggingService.LogError("StartAccountAsync", ex);
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

        private async Task ExecutePurchaseAsync(Account account, Configuration config)
        {
            try
            {
                if (config.PurchaseMode == PurchaseMode.ScanMode)
                {
                    // Scan mode: check product availability periodically
                }
                else
                {
                    // Fixed time purchase mode
                    await ExecuteFixedPurchaseAsync(account, config);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ExecutePurchaseAsync", ex);
            }
        }

        private async Task ExecuteFixedPurchaseAsync(Account account, Configuration config)
        {
            IBrowser? browser = null;
            try
            {
                // Đợi đến trước thời gian mua hàng 5 phút để login trước
                //await WaitToFixedTime(config, timeRunBeforeSecond * -1);

                var accountId = await _puppeteerService.LaunchBrowserForAccountAsync(account);

                var strategy = _strategies[config.SelectedWebsite];
                var page = _puppeteerService.GetPageForAccount(accountId);
                browser = _puppeteerService.GetBrowserForAccount(accountId);

                if (page == null)
                    return;

                // Login if not already logged in
                UpdateStatus(account, "Đang đăng nhập");
                (bool loginSuccess, FailedReason code) = await strategy.LoginAsync(page, browser, account);
                if (!loginSuccess)
                {
                    var message = code switch
                    {
                        FailedReason.PasswordIncorrect => "Sai mk hoặc ip bị chặn login",
                        FailedReason.ProxyError => "Proxy lỗi",
                        FailedReason.UnknownError => "Lỗi chưa xác định",
                        _ => "Login failed"
                    };
                    account.Status = AccountStatus.Failed;
                    UpdateStatus(account, message);
                    return;
                }
                UpdateStatus(account, "Login xong, đang chờ...");
                // Khi login xong, đợi đến tg mua hàng

                //await WaitToFixedTime(config);

                UpdateStatus(account, "Bắt đầu mua hàng");
                var productIds = config.ProductIds;

                //(bool purchaseSuccess, FailedReason purchaseCode) = await strategy.PurchaseProductAsync(page, productIds, account, config, 1);

                (bool purchaseSuccess, FailedReason purchaseCode) = (true, FailedReason.None); // Simulate purchase success for testing

                account.Status = purchaseSuccess ? AccountStatus.Success : AccountStatus.Failed;
                var messagePurchase = purchaseCode switch
                {
                    FailedReason.OutOfStock => "Sản phẩm không còn hàng",
                    FailedReason.UnknownError => "Lỗi chưa xác định",
                    _ => "Purchase failed"
                };
                messagePurchase = purchaseSuccess ? "Mua hàng thành công" : messagePurchase;
                account.StatusText = messagePurchase;
                OnAccountStatusChanged(account);

                // Stop this account after successful purchase
                await StopAccountAsync(account);
                return;
            }
            catch (Exception ex)
            {
                account.Status = AccountStatus.Failed;
                OnAccountStatusChanged(account);
                // Log error
                _loggingService.LogError("ExecutePurchaseAsync", ex);
            }
            finally
            {
                try
                {
                    if (browser != null)
                    {
                        await browser.CloseAsync();
                    }
                }
                catch { }
            }
        }

        private async Task WaitToFixedTime(Configuration config, int? time = null)
        {
            if (!config.FixedTime.HasValue)
            {
                return;
            }

            var fixedDate = new DateTime(
                DateTime.Now.Year,
                DateTime.Now.Month,
                DateTime.Now.Day,
                config.FixedTime.Value.Hour,
                config.FixedTime.Value.Minute,
                config.FixedTime.Value.Second
            );

            if (time.HasValue)
            {
                fixedDate = fixedDate.AddSeconds(time.Value);
            }

            var timeDelay = fixedDate - DateTime.Now;
            await Task.Delay(timeDelay > TimeSpan.Zero ? timeDelay : TimeSpan.Zero);
        }

        private async Task<List<string>> WaitToProductsAvailable(Configuration config)
        {
            var delay = config.ScanSecond.HasValue ? config.ScanSecond.Value * 1000 : 5000;
            while (true)
            {
                // TODO: check product availability
                await Task.Delay((int)delay);
                break;
            }

            return config.ProductIds;
        }

        private void UpdateStatus(Account account, string message)
        {
            account.StatusText = message;
            OnAccountStatusChanged(account);
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
