using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.AnonymizeUa;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;

var extra = new PuppeteerExtra()
    .Use(new StealthPlugin())
    .Use(new AnonymizeUaPlugin());

var options = new BrowserFetcherOptions();
options.Path = Path.Combine("./App_Data");
var bf = new BrowserFetcher(options);
await bf.DownloadAsync();
string exePath = bf.GetInstalledBrowsers().First().GetExecutablePath();

var browser = await extra.LaunchAsync(new LaunchOptions
{
    Headless = false,
    ExecutablePath = exePath,
});

var page = await browser.NewPageAsync();
await page.GoToAsync("https://www.yodobashi.com/");