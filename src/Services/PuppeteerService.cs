using BanYodo.Models;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.AnonymizeUa;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;

namespace BanYodo.Services
{
    public class PuppeteerService
    {
        private readonly string _browserPath;
        private readonly Dictionary<string, IBrowser> _browsers;
        private readonly Dictionary<string, IPage> _pages;

        public PuppeteerService()
        {
            _browserPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            _browsers = new Dictionary<string, IBrowser>();
            _pages = new Dictionary<string, IPage>();
        }

        public async Task InitializeBrowserAsync()
        {
            try
            {
                var options = new BrowserFetcherOptions();
                options.Path = _browserPath;
                var browserFetcher = new BrowserFetcher(options);
                
                if (!browserFetcher.GetInstalledBrowsers().Any())
                {
                    await browserFetcher.DownloadAsync();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize browser: {ex.Message}", ex);
            }
        }

        public async Task<string> LaunchBrowserForAccountAsync(Account account)
        {
            try
            {
                var accountId = $"{account.Username}_{Guid.NewGuid():N}";
                
                var extra = new PuppeteerExtra()
                    .Use(new StealthPlugin())
                    .Use(new AnonymizeUaPlugin());

                var options = new BrowserFetcherOptions();
                options.Path = _browserPath;
                var browserFetcher = new BrowserFetcher(options);
                string executablePath = browserFetcher.GetInstalledBrowsers().First().GetExecutablePath();

                var launchOptions = new LaunchOptions
                {
                    Headless = false,
                    ExecutablePath = executablePath,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                };

                // Add proxy if specified
                if (!string.IsNullOrWhiteSpace(account.Proxy))
                {
                    var proxyParts = account.Proxy.Split(':');
                    if (proxyParts.Length >= 2)
                    {
                        var proxyArgs = new List<string>(launchOptions.Args ?? new string[0]);
                        proxyArgs.Add($"--proxy-server={proxyParts[0]}:{proxyParts[1]}");
                        launchOptions.Args = proxyArgs.ToArray();
                    }
                }

                var browser = await extra.LaunchAsync(launchOptions);
                var page = await browser.NewPageAsync();

                // Handle proxy authentication if username/password provided
                if (!string.IsNullOrWhiteSpace(account.Proxy))
                {
                    var proxyParts = account.Proxy.Split(':');
                    if (proxyParts.Length == 4)
                    {
                        await page.AuthenticateAsync(new Credentials
                        {
                            Username = proxyParts[2],
                            Password = proxyParts[3]
                        });
                    }
                }

                _browsers[accountId] = browser;
                _pages[accountId] = page;

                return accountId;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to launch browser for account {account.Username}: {ex.Message}", ex);
            }
        }

        public async Task NavigateToWebsiteAsync(string accountId, Website website)
        {
            if (!_pages.ContainsKey(accountId))
                throw new InvalidOperationException($"No browser session found for account ID: {accountId}");

            try
            {
                var page = _pages[accountId];
                string url = website switch
                {
                    Website.Yodobashi => "https://www.yodobashi.com/",
                    Website.Fujifilm => "https://fujifilm.com/", // Placeholder
                    _ => throw new NotSupportedException($"Website {website} is not supported yet")
                };

                await page.GoToAsync(url);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to navigate to {website}: {ex.Message}", ex);
            }
        }

        public async Task CloseBrowserAsync(string accountId)
        {
            try
            {
                if (_pages.ContainsKey(accountId))
                {
                    await _pages[accountId].CloseAsync();
                    _pages.Remove(accountId);
                }

                if (_browsers.ContainsKey(accountId))
                {
                    await _browsers[accountId].CloseAsync();
                    _browsers.Remove(accountId);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw to avoid blocking cleanup
            }
        }

        public async Task CloseAllBrowsersAsync()
        {
            var accountIds = _browsers.Keys.ToList();
            foreach (var accountId in accountIds)
            {
                await CloseBrowserAsync(accountId);
            }
        }

        public IPage? GetPageForAccount(string accountId)
        {
            return _pages.ContainsKey(accountId) ? _pages[accountId] : null;
        }
        public IBrowser? GetBrowserForAccount(string accountId)
        {
            return _browsers.ContainsKey(accountId) ? _browsers[accountId] : null;
        }

        public bool HasActiveBrowser(string accountId)
        {
            return _browsers.ContainsKey(accountId) && _pages.ContainsKey(accountId);
        }

        public void Dispose()
        {
            Task.Run(async () => await CloseAllBrowsersAsync());
        }
    }
}
