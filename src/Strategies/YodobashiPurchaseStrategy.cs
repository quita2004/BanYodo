using BanYodo.Models;
using BanYodo.Services;
using Newtonsoft.Json;
using PuppeteerSharp;
using RestSharp;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using HtmlNode = HtmlAgilityPack.HtmlNode;

namespace BanYodo.Strategies
{
    public class NetworkGetAllCookiesResponse
    {
        public CookieParam[] Cookies { get; set; }
    }

    public class CookieParam
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; }
        public string Path { get; set; }
        public bool Secure { get; set; }
        public bool HttpOnly { get; set; }
        public double Expires { get; set; }
        public int Size { get; set; }
        public bool Session { get; set; }
        public string SameSite { get; set; }
    }
    public class YodobashiPurchaseStrategy : IPurchaseStrategy
    {
        private const int TIME_CHECK_PRODUCT_AVAILABLE = 300;
        private const double TIME_WAIT = 0.1;
        private const bool NOT_ONLY_USE_PROXY_CONFIRM = true;

        private RestClient _restClient;
        private LoggingService _loggingService;
        private List<Cookie> _cookies = new List<Cookie>();
        private Random _random = new Random();
        private bool _useOldCard = true;

        public Website SupportedWebsite => Website.Yodobashi;

        public YodobashiPurchaseStrategy()
        {
            var options = new RestClientOptions()
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36",
                FollowRedirects = false, // Handle redirects manually like JS version
                Timeout = TimeSpan.FromSeconds(60),
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };

            _restClient = new RestClient(options);

            // Configure TLS to match browser behavior
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            _loggingService = new LoggingService();
        }

        public async Task<(bool, FailedReason)> LoginAsync(IPage page, IBrowser browser, Account account)
        {
            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"{_loggingService.GetCurrentTime()}Attempting to login for account: {account.Username}");
            try
            {
                // Check if already logged in
                if (IsLoggedInAsync(page))
                    return (true, FailedReason.None);

                // Navigate to Yodobashi home page
                if (!await GoToYodoHomeAsync(page))
                {
                    logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Failed to navigate to Yodobashi home page");
                    return (false, FailedReason.ProxyError);
                }

                logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Go home page successfully");
                // Click login link
                await ClickElementAsync(page, "#logininfo a.cl-hdLO2_1");

                await page.WaitForSelectorAsync("#memberId");
                await DelayRandomAsync(10000, 5000);

                // Fill login form with natural typing
                await page.ClickAsync("#memberId");
                await page.TypeAsync("#memberId", account.Username);
                await DelayRandomAsync(3000, 1000);

                await page.ClickAsync("#password");
                await page.TypeAsync("#password", account.Password);
                await DelayRandomAsync(3000, 1000);

                // Submit login
                await page.ClickAsync("#js_i_login0");

                // đợi trang load xong
                await page.WaitForNavigationAsync();

                // Check login result
                var pageContent = await page.GetContentAsync();
                var loginSuccess = !pageContent.Contains("正しく入力されていない項目があります。メッセージをご確認の上、もう一度ご入力ください");

                logMessage.AppendLine($"{_loggingService.GetCurrentTime()}Login {(loginSuccess ? "succeeded" : "failed")} for account: {account.Username}");
                if (loginSuccess)
                {
                    await DelayRandomAsync(5000, 3000);
                    //var pageCookies = await page.GetCookiesAsync(["https://www.yodobashi.com", "https://order.yodobashi.com"]);
                    var client = await page.Target.CreateCDPSessionAsync();
                    var cookiesResponse = await client.SendAsync<NetworkGetAllCookiesResponse>("Network.getAllCookies");

                    _cookies = cookiesResponse.Cookies.Select(c => new Cookie
                    {
                        Name = c.Name,
                        Value = c.Value,
                        Domain = c.Domain,
                        Path = c.Path
                    }).ToList();

                    return (true, FailedReason.None);
                }

                // Step 1: Clear cart and delete old card if needed
                await ClearCartAsync();
                logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Cleared cart for product: {account.Username}");

                if (!_useOldCard)
                {
                    await DeleteDefaultCardAsync();
                }

                return (false, FailedReason.PasswordIncorrect);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Login failed for account: {account.Username}", ex);
                // Log error
                return (false, FailedReason.UnknownError);
            }
            finally
            {
                try
                {
                    _loggingService.LogInfo(logMessage.ToString(), "LoginAsync");
                    if (browser is not null)
                    {
                        await browser.CloseAsync();
                    }
                }
                catch { }
            }
        }

        public async Task<bool> CheckProductAvailabilityAsync(IPage page, string productId)
        {
            try
            {
                // Use HTTP request to check availability (faster than browser)
                var location = await CallApiAddCartAsync(productId, 1);
                return !location.Contains("error");
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool, FailedReason)> PurchaseProductAsync(IPage page, List<string> productIds, Account account, Configuration configuration, int amount = 1)
        {
            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"Starting purchase for account: {account.Username}");
            try
            {
                // Step 2: Add product to cart with retry logic
                string location = "error";
                int countSuccess = 0;
                foreach (var productId in productIds)
                {
                    var countTryAddCard = 0;

                    // Nếu scan mode thì chỉ gọi thêm giỏ hàng 1 lần rồi chuyển sang sp tiếp theo
                    var timeTryAdd = TIME_CHECK_PRODUCT_AVAILABLE;
                    if (configuration.PurchaseMode == PurchaseMode.ScanMode)
                    {
                        timeTryAdd = 1;
                    }

                    while (location.Contains("error") && countTryAddCard < timeTryAdd)
                    {
                        countTryAddCard++;
                        await Task.Delay((int)(TIME_WAIT * 1000));
                        location = await CallApiAddCartAsync(productId, amount);
                        logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Adding product to cart: {productId}, success: {!location.Contains("error")}");
                    }

                    if (!location.Contains("error"))
                    {
                        countSuccess++;
                    }
                }

                if(countSuccess == 0)
                {
                    return (false, FailedReason.AddCartFailed);
                }

                await SleepRandomAsync();

                // Step 3: Proceed to checkout
                var resultCalNextCart = await CallNextCartAsync();
                logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call next cart");

                await SleepRandomAsync();

                location = await CallPaymentAsync(resultCalNextCart);
                logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call payment: {location}");

                await SleepRandomAsync();

                location = await CallGetOrderIndexAsync(location);
                logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call get order index");
                await SleepRandomAsync();

                var nodeStateKey = GetParamValue("nodeStateKey", location);

                // Step 4: Handle payment information
                var hasOldCard = false;
                var postToken = "";

                if (location.Contains("reinputcredit"))
                {
                    postToken = await GetReinputIndexAsync(nodeStateKey);

                    if (!_useOldCard)
                    {
                        location = await CallReinputCreditAsync(postToken, nodeStateKey);
                    }
                    else
                    {
                        location = await CallReinputCreditAsync(postToken, nodeStateKey,
                            account.Card, int.Parse(account.CardMonth), int.Parse(account.CardYear), "true");
                        location = await CallOrderNextAsync(location, nodeStateKey);
                        hasOldCard = true;
                    }
                }
                else if (location.Contains("order/confirm/index.html"))
                {
                    hasOldCard = true;
                }

                if (!hasOldCard)
                {
                    // Handle new card payment
                    var resultPayment = await CallGetPaymentIndexAsync(location);
                    logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call get payment index");
                    postToken = resultPayment.PostToken;
                    await SleepRandomAsync();

                    // Tokenize card information
                    var cardNumber = await GetPanTokenAsync(nodeStateKey, postToken, account.Card);
                    logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call get pan token");

                    location = await CallPostPaymentAsync(resultPayment, cardNumber,
                        int.Parse(account.CardMonth), int.Parse(account.CardYear));
                    logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call post payment");
                    await SleepRandomAsync();

                    location = await CallPaymentNextAsync(location, nodeStateKey);
                    logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call payment next");
                    await SleepRandomAsync();
                }

                // Step 5: Confirm order
                var resultConfirm = await CallGetConfirmAsync(location);
                logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call get confirm");

                // Handle Akamai script
                await CallAkamaiScriptAsync(resultConfirm.AkamaiScriptUrl, nodeStateKey);

                // Get delivery information
                var resultDelivery = await GetDeliveryAsync(resultConfirm);
                logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call get delivery");
                await SleepRandomAsync();

                // Merge delivery info
                var finalConfirm = MergeConfirmAndDelivery(resultConfirm, resultDelivery);

                // Submit final order
                location = await CallPostConfirmAsync(finalConfirm, account.CardCvv, account.Username);
                logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Call post confirm");
                await SleepRandomAsync();

                // Check completion
                var (isSuccess, html) = await CallCompleteAsync(finalConfirm.NodeStateKey);
                logMessage.AppendLine($"{_loggingService.GetCurrentTime()} Buy success:{isSuccess}");
                if (!isSuccess)
                {
                    SaveHtmlLog(account.Username, html);
                }

                return (isSuccess, FailedReason.OutOfStock);
            }
            catch (Exception ex)
            {
                // Log error
                _loggingService.LogError("PurchaseProductAsync", ex);
                return (false, FailedReason.UnknownError);
            }
            finally
            {
                _loggingService.LogInfo(logMessage.ToString(), "PurchaseProductAsync");
            }
        }

        public bool IsLoggedInAsync(IPage page)
        {
            try
            {
                return _cookies is not null && _cookies.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        #region Private Helper Methods

        private async Task<bool> GoToYodoHomeAsync(IPage page)
        {
            try
            {
                await page.GoToAsync("https://yodobashi.com");
                await DelayRandomAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task ClickElementAsync(IPage page, string selector)
        {
            await page.WaitForSelectorAsync(selector);
            await page.ClickAsync(selector);
        }

        private async Task DelayRandomAsync(int maxMs = 10, int min = 0)
        {
            await Task.Delay(_random.Next(0, maxMs));
        }

        private async Task SleepRandomAsync()
        {
            await Task.Delay(_random.Next(10, 20));
        }

        private int GetRandom(int max, int min = 0)
        {
            return _random.Next(min, max);
        }

        private string GetParamValue(string paramName, string url)
        {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);
            return query[paramName] ?? string.Empty;
        }

        private string GetCookiesString()
        {
            var result = new List<string>();
            for (int index = 0; index < _cookies.Count; index++)
            {
                var element = _cookies[index];
                result.Add($"{element.Name}={element.Value}");
            }
            return string.Join(";", result); // Add space after semicolon like JS version
        }

        private void UpdateCookies(RestResponse response)
        {
            if (response.Headers != null)
            {
                var setCookieHeaders = response.Headers.Where(h => h.Name?.ToLower() == "set-cookie");
                foreach (var header in setCookieHeaders)
                {
                    if (header.Value != null)
                    {
                        var element = header.Value.ToString();
                        var content = element.Split(';')[0];
                        var keyValue = content.Split('=');

                        // Handle values with = sign (like in JS version)
                        if (keyValue.Length > 2)
                        {
                            for (int i = 2; i < keyValue.Length; i++)
                            {
                                keyValue[1] += "=" + keyValue[i];
                            }
                        }

                        // Update existing cookie or add new one (like JS version)
                        var existingCookie = _cookies.FirstOrDefault(x => x.Name == keyValue[0]);
                        if (existingCookie != null)
                        {
                            existingCookie.Value = keyValue[1];
                        }
                        else
                        {
                            _cookies.Add(new Cookie
                            {
                                Name = keyValue[0],
                                Value = keyValue[1]
                            });
                        }
                    }
                }
            }
        }

        #endregion

        #region API Call Methods

        private async Task<string> CallApiAddCartAsync(string productId, int amount)
        {
            try
            {
                // Add random delay before request (like JS version)
                await Task.Delay(_random.Next(100, 500));

                var request = new RestRequest("https://order.yodobashi.com/yc/shoppingcart/add/index.html", Method.Post);

                // Add headers exactly like JavaScript version
                request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
                request.AddHeader("Accept-Encoding", "gzip, deflate, br, zstd");
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Connection", "keep-alive");
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddHeader("Origin", "https://www.yodobashi.com");
                request.AddHeader("Pragma", "no-cache");
                request.AddHeader("Referer", "https://www.yodobashi.com/");
                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");
                request.AddHeader("Sec-Fetch-Dest", "document");
                request.AddHeader("Sec-Fetch-Mode", "navigate");
                request.AddHeader("Sec-Fetch-Site", "same-site");
                request.AddHeader("Sec-Fetch-User", "?1");
                request.AddHeader("Upgrade-Insecure-Requests", "1");
                request.AddHeader("sec-ch-ua", "\"Chromium\";v=\"134\", \"Not:A-Brand\";v=\"24\", \"Google Chrome\";v=\"134\"");
                request.AddHeader("sec-ch-ua-mobile", "?0");
                request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
                request.AddHeader("Cookie", GetCookiesString());

                // Add form data exactly like JavaScript version
                request.AddParameter("postCode", "0791134");
                request.AddParameter("returnUrl", $"https://www.yodobashi.com/product/{productId}/index.html");
                request.AddParameter("products[0].cartInSKU", productId);
                request.AddParameter("products[0].itemId", productId);
                request.AddParameter("products[0].serviceFlag", "0");
                request.AddParameter("products[0].amount", amount.ToString());
                request.AddParameter("products[0].price", "0");
                request.AddParameter("products[0].encryptPrice", "ffeadb50e7afbc4e");
                request.AddParameter("products[0].pointRate", "0");
                request.AddParameter("products[0].encryptPointRate", "74ab67277c012bf7");
                request.AddParameter("products[0].salesInformationCode", "0027");
                request.AddParameter("products[0].salesReleaseDay", "2018/05/25");
                request.AddParameter("products[0].salesReleaseDayString", "");
                request.AddParameter("products[0].stockStatusCode", "0002");
                request.AddParameter("products[0].isDownload", "false");
                request.AddParameter("products[0].readCheckFlg", "0");

                var response = await _restClient.ExecuteAsync(request);
                UpdateCookies(response);

                if (response.Headers?.FirstOrDefault(h => h.Name == "Location")?.Value != null)
                {
                    return response.Headers.First(h => h.Name == "Location").Value.ToString();
                }

                return "error";
            }
            catch (Exception ex)
            {
                return "error";
            }
        }

        private async Task<NextCartResult> CallNextCartAsync(int retry = 0)
        {
            try
            {
                var request = new RestRequest("https://order.yodobashi.com/yc/shoppingcart/index.html?next=true", Method.Get);

                // Add all headers to match JavaScript version
                request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Connection", "keep-alive");
                request.AddHeader("Pragma", "no-cache");
                request.AddHeader("Referer", "https://order.yodobashi.com/");
                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
                request.AddHeader("Cookie", GetCookiesString());
                request.AddHeader("Sec-Fetch-Dest", "document");
                request.AddHeader("Sec-Fetch-Mode", "navigate");
                request.AddHeader("Sec-Fetch-Site", "same-origin");
                request.AddHeader("Sec-Fetch-User", "?1");
                request.AddHeader("Upgrade-Insecure-Requests", "1");
                request.AddHeader("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"");
                request.AddHeader("sec-ch-ua-mobile", "?0");
                request.AddHeader("sec-ch-ua-platform", "\"Windows\"");

                var response = await _restClient.ExecuteAsync(request);
                UpdateCookies(response);

                var doc = new HtmlDocument();
                doc.LoadHtml(response.Content);

                return new NextCartResult
                {
                    PostToken = GetInputValue(doc, "postToken"),
                    DetailNo = GetInputValue(doc, "ordinaryProducts[0].detailNo"),
                    Editable = GetInputValue(doc, "ordinaryProducts[0].editable"),
                    Amount = GetInputValue(doc, "ordinaryProducts[0].amount")
                };
            }
            catch (Exception ex)
            {
                if (retry < 10)
                {
                    await Task.Delay(GetRandom(200, 100));
                    return await CallNextCartAsync(retry + 1);
                }
                throw;
            }
        }

        private async Task<string> CallPaymentAsync(NextCartResult cartResult, int retry = 0)
        {
            try
            {
                var request = new RestRequest("https://order.yodobashi.com/yc/shoppingcart/action.html", Method.Post);

                // Add all headers to match JavaScript version
                request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Connection", "keep-alive");
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddHeader("Origin", "https://order.yodobashi.com");
                request.AddHeader("Pragma", "no-cache");
                request.AddHeader("Referer", "https://order.yodobashi.com/");
                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
                request.AddHeader("Cookie", GetCookiesString());
                request.AddHeader("Sec-Fetch-Dest", "document");
                request.AddHeader("Sec-Fetch-Mode", "navigate");
                request.AddHeader("Sec-Fetch-Site", "same-origin");
                request.AddHeader("Sec-Fetch-User", "?1");
                request.AddHeader("Upgrade-Insecure-Requests", "1");
                request.AddHeader("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"");
                request.AddHeader("sec-ch-ua-mobile", "?0");
                request.AddHeader("sec-ch-ua-platform", "\"Windows\"");

                request.AddParameter("postToken", cartResult.PostToken);
                request.AddParameter("ordinaryProducts[0].detailNo", cartResult.DetailNo);
                request.AddParameter("ordinaryProducts[0].editable", cartResult.Editable);
                request.AddParameter("ordinaryProducts[0].amount", cartResult.Amount);
                request.AddParameter("order", "order");

                var response = await _restClient.ExecuteAsync(request);
                UpdateCookies(response);

                return response.Headers?.FirstOrDefault(h => h.Name == "Location")?.Value?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                if (retry < 10)
                {
                    await Task.Delay(GetRandom(200, 100));
                    return await CallPaymentAsync(cartResult, retry + 1);
                }
                throw;
            }
        }

        private async Task<string> CallGetOrderIndexAsync(string url)
        {
            var request = new RestRequest(url, Method.Get);

            // Add all headers to match JavaScript version
            request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Pragma", "no-cache");
            request.AddHeader("Referer", "https://order.yodobashi.com/");
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
            request.AddHeader("Cookie", GetCookiesString());
            request.AddHeader("Sec-Fetch-Dest", "document");
            request.AddHeader("Sec-Fetch-Mode", "navigate");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("Sec-Fetch-User", "?1");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            request.AddHeader("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            return response.Headers?.FirstOrDefault(h => h.Name == "Location")?.Value?.ToString() ?? "";
        }

        private async Task<string> GetReinputIndexAsync(string nodeStateKey)
        {
            var request = new RestRequest($"https://order.yodobashi.com/yc/order/reinputcredit/index.html?nodeStateKey={nodeStateKey}", Method.Get);

            request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.AddHeader("Accept-Language", "en,en-US;q=0.9");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Sec-Fetch-Dest", "document");
            request.AddHeader("Sec-Fetch-Mode", "navigate");
            request.AddHeader("Sec-Fetch-Site", "none");
            request.AddHeader("Sec-Fetch-User", "?1");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");
            request.AddHeader("sec-ch-ua", "\"Chromium\";v=\"134\", \"Not:A-Brand\";v=\"24\", \"Google Chrome\";v=\"134\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("Cookie", GetCookiesString());

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            var doc = new HtmlDocument();
            doc.LoadHtml(response.Content);

            return GetInputValue(doc, "postToken");
        }

        private async Task<string> CallReinputCreditAsync(string postToken, string nodeStateKey,
            string cardNo = "", int limitMonth = 0, int limitYear = 0, string selectCredit = "false")
        {
            var request = new RestRequest("https://order.yodobashi.com/yc/order/reinputcredit/action.html", Method.Post);

            request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.AddHeader("Accept-Language", "en,en-US;q=0.9");
            request.AddHeader("Cache-Control", "max-age=0");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddHeader("Origin", "https://order.yodobashi.com");
            request.AddHeader("Referer", $"https://order.yodobashi.com/yc/order/reinputcredit/index.html?nodeStateKey={nodeStateKey}");
            request.AddHeader("Sec-Fetch-Dest", "document");
            request.AddHeader("Sec-Fetch-Mode", "navigate");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("Sec-Fetch-User", "?1");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");
            request.AddHeader("sec-ch-ua", "\"Chromium\";v=\"134\", \"Not:A-Brand\";v=\"24\", \"Google Chrome\";v=\"134\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("Cookie", GetCookiesString());

            request.AddParameter("postToken", postToken);
            request.AddParameter("nodeStateKey", nodeStateKey);
            request.AddParameter("cardNo", cardNo);
            request.AddParameter("limitMonth", limitMonth > 0 ? limitMonth.ToString() : " ");
            request.AddParameter("limitYear", limitYear > 0 ? limitYear.ToString() : " ");
            request.AddParameter("selectCredit", selectCredit);
            request.AddParameter("next", "next");

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            return response.Headers?.FirstOrDefault(h => h.Name == "Location")?.Value?.ToString() ?? "";
        }

        private async Task<string> CallOrderNextAsync(string url, string nodeStateKey)
        {
            var request = new RestRequest(url, Method.Get);
            AddStandardGetHeaders(request, $"https://order.yodobashi.com/yc/order/reinputcredit/index.html?nodeStateKey={nodeStateKey}");

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            return response.Headers?.FirstOrDefault(h => h.Name == "Location")?.Value?.ToString() ?? "";
        }

        private async Task<PaymentResult> CallGetPaymentIndexAsync(string url)
        {
            var request = new RestRequest(url, Method.Get);
            AddStandardGetHeaders(request, "https://order.yodobashi.com/yc/shoppingcart/index.html?returnUrl=https%3A%2F%2Fwww.yodobashi.com%2F");

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            var doc = new HtmlDocument();
            doc.LoadHtml(response.Content);

            return new PaymentResult
            {
                PostToken = GetInputValue(doc, "postToken"),
                NodeStateKey = GetInputValue(doc, "nodeStateKey"),
                Balance = GetInputValue(doc, "balance"),
                Total = GetInputValue(doc, "total"),
                PointPaymentTypeCode = GetInputValue(doc, "pointPaymentTypeCode"),
                PaymentTypeCode0 = GetInputValue(doc, "paymentDetails[0].paymentTypeCode")
            };
        }

        private Task<string> GetPanTokenAsync(string nodeStateKey, string postToken, string cardNumber)
        {
            try
            {
                // Simplified tokenization - in real implementation, this would involve
                // multiple API calls for access token, tokenization, and decryption
                return Task.FromResult(cardNumber); // Placeholder
            }
            catch
            {
                return Task.FromResult(cardNumber);
            }
        }

        private async Task<string> CallPostPaymentAsync(PaymentResult paymentResult, string cardNumber,
            int cardMonth, int cardYear)
        {
            var request = new RestRequest("https://order.yodobashi.com/yc/order/payment/action.html", Method.Post);

            AddStandardPostHeaders(request, "https://order.yodobashi.com", $"https://order.yodobashi.com/yc/order/payment/index.html?nodeStateKey={paymentResult.NodeStateKey}");

            // Add payment parameters based on card type
            var isOtherCard = paymentResult.PaymentTypeCode0.StartsWith("CEN");

            if (_useOldCard)
            {
                request.AddParameter("alwaysUse", "true");
            }

            // Add all required payment fields
            request.AddParameter("postToken", paymentResult.PostToken);
            request.AddParameter("nodeStateKey", paymentResult.NodeStateKey);
            request.AddParameter("_alwaysUse", "on");
            request.AddParameter("balance", paymentResult.Balance);
            request.AddParameter("total", paymentResult.Total);
            request.AddParameter("pointPaymentTypeCode", paymentResult.PointPaymentTypeCode);
            request.AddParameter("goldPointForUse", "");
            request.AddParameter("paymentTypeCode", "002");
            request.AddParameter("paymentDetails[0].paymentTypeCode", "002");
            request.AddParameter("paymentDetails[0].invalid", "true");
            request.AddParameter("paymentDetails[0].cardNo", cardNumber);
            request.AddParameter("paymentDetails[0].limitMonth", cardMonth.ToString());
            request.AddParameter("paymentDetails[0].limitYear", cardYear.ToString());
            request.AddParameter("next", "next");

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            return response.Headers?.FirstOrDefault(h => h.Name == "Location")?.Value?.ToString() ?? "";
        }

        private async Task<string> CallPaymentNextAsync(string url, string nodeStateKey)
        {
            var request = new RestRequest(url, Method.Get);
            AddStandardGetHeaders(request, $"https://order.yodobashi.com/yc/order/payment/index.html?nodeStateKey={nodeStateKey}");

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            return response.Headers?.FirstOrDefault(h => h.Name == "Location")?.Value?.ToString() ?? "";
        }

        private async Task<ConfirmResult> CallGetConfirmAsync(string url, int retry = 0)
        {
            try
            {
                var request = new RestRequest(url, Method.Get);
                AddStandardGetHeaders(request, "https://order.yodobashi.com/");

                var response = await _restClient.ExecuteAsync(request);
                UpdateCookies(response);

                var doc = new HtmlDocument();
                doc.LoadHtml(response.Content);

                return new ConfirmResult
                {
                    PostToken = GetInputValue(doc, "postToken"),
                    NodeStateKey = GetInputValue(doc, "nodeStateKey"),
                    AkamaiScriptUrl = GetAkamaiScript(doc)
                };
            }
            catch (Exception ex)
            {
                if (retry < 10)
                {
                    await Task.Delay(GetRandom(200, 100));
                    return await CallGetConfirmAsync(url, retry + 1);
                }
                throw;
            }
        }

        private async Task CallAkamaiScriptAsync(string url, string nodeStateKey, int count = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return;

                if (!url.StartsWith("https://order.yodobashi.com"))
                {
                    url = "https://order.yodobashi.com" + url;
                }

                var request = new RestRequest(url, Method.Get);
                request.AddHeader("Accept", "*/*");
                request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Connection", "keep-alive");
                request.AddHeader("Pragma", "no-cache");
                request.AddHeader("Referer", $"https://order.yodobashi.com/yc/order/confirm/index.html?nodeStateKey={nodeStateKey}");
                request.AddHeader("Sec-Fetch-Dest", "script");
                request.AddHeader("Sec-Fetch-Mode", "no-cors");
                request.AddHeader("Sec-Fetch-Site", "same-origin");
                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
                request.AddHeader("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"");
                request.AddHeader("sec-ch-ua-mobile", "?0");
                request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
                request.AddHeader("Cookie", GetCookiesString());

                var response = await _restClient.ExecuteAsync(request);
                UpdateCookies(response);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound && count <= 5)
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(response.Content);
                    var nextAkamaiUrl = GetAkamaiScript(doc);

                    if (!string.IsNullOrEmpty(nextAkamaiUrl))
                    {
                        await CallAkamaiScriptAsync(nextAkamaiUrl, nodeStateKey, count + 1);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue
            }
        }

        private async Task<DeliveryResult> GetDeliveryAsync(ConfirmResult confirmResult, int retry = 0)
        {
            try
            {
                var request = new RestRequest("https://order.yodobashi.com/yc/order/confirm/ajax/deliveryChange.html", Method.Post);

                request.AddHeader("Accept", "application/json, text/javascript, */*; q=0.01");
                request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Connection", "keep-alive");
                request.AddHeader("Content-Type", "application/json;");
                request.AddHeader("Origin", "https://order.yodobashi.com");
                request.AddHeader("Pragma", "no-cache");
                request.AddHeader("Referer", $"https://order.yodobashi.com/yc/order/confirm/index.html?nodeStateKey={confirmResult.NodeStateKey}");
                request.AddHeader("Sec-Fetch-Dest", "empty");
                request.AddHeader("Sec-Fetch-Mode", "cors");
                request.AddHeader("Sec-Fetch-Site", "same-origin");
                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
                request.AddHeader("X-Requested-With", "XMLHttpRequest");
                request.AddHeader("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"");
                request.AddHeader("sec-ch-ua-mobile", "?0");
                request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
                request.AddHeader("Cookie", GetCookiesString());

                var data = new
                {
                    nodeStateKey = confirmResult.NodeStateKey,
                    postToken = confirmResult.PostToken,
                    loadDemand = false
                };

                request.AddJsonBody(data);

                var response = await _restClient.ExecuteAsync(request);
                UpdateCookies(response);

                var jsonResponse = JsonConvert.DeserializeObject<dynamic>(response.Content);
                var html = jsonResponse?.result?.data?.productListHtml?.ToString();

                if (!string.IsNullOrEmpty(html))
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    return new DeliveryResult
                    {
                        DetailNo = GetInputValue(doc, "originalProductDetails[0].detailNo"),
                        DeliveryDateTypeSelect = GetInputValue(doc, "deliveries[0].deliveryDateTypeSelect"),
                        ShortestDate = GetInputValue(doc, "deliveries[0].shortestDate"),
                        StartTime = GetInputValue(doc, "answerToPromiseSearch[0].startTime"),
                        EndTime = GetInputValue(doc, "answerToPromiseSearch[0].endTime"),
                        DefaultDate = GetInputValue(doc, "answerToPromiseSearch[0].defaultDate"),
                        DeliveryDateSelect = GetInputValue(doc, "deliveries[0].deliveryDateSelect"),
                        DeliveryMethodSelect = GetInputValue(doc, "deliveryMethodSelect")
                    };
                }

                return new DeliveryResult();
            }
            catch (Exception ex)
            {
                if (retry < 10)
                {
                    await Task.Delay(GetRandom(200, 100));
                    return await GetDeliveryAsync(confirmResult, retry + 1);
                }
                throw;
            }
        }

        private async Task<string> CallPostConfirmAsync(FinalConfirmResult data, string cardCvv, string userName, int retry = 0)
        {
            try
            {
                var request = new RestRequest("https://order.yodobashi.com/yc/order/confirm/action.html", Method.Post);

                AddStandardPostHeaders(request, "https://order.yodobashi.com", $"https://order.yodobashi.com/yc/order/confirm/index.html?nodeStateKey={data.NodeStateKey}");

                // Add all confirmation parameters
                request.AddParameter("postToken", data.PostToken);
                request.AddParameter("nodeStateKey", data.NodeStateKey);
                request.AddParameter("creditCard.paymentNumberIndex", "01");
                request.AddParameter("creditCard.securityCode", cardCvv);
                request.AddParameter("_receiptReceive", "on");
                request.AddParameter("_receiptMailReceive", "on");
                request.AddParameter("receiptName", "");
                request.AddParameter("deliveryMethodSelect", data.DeliveryMethodSelect);
                request.AddParameter("y", data.DeliveryMethodSelect);
                request.AddParameter("originalProductDetails[0].selectedKeys", "");
                request.AddParameter("originalProductDetails[0].detailNo", data.DetailNo);
                request.AddParameter("deliveries[0].deliveryDateTypeSelect", data.DeliveryDateTypeSelect);
                request.AddParameter("deliveries[0].shortestDate", data.ShortestDate);
                request.AddParameter("answerToPromiseSearch[0].startTime", data.StartTime);
                request.AddParameter("answerToPromiseSearch[0].endTime", data.EndTime);
                request.AddParameter("answerToPromiseSearch[0].defaultDate", data.DefaultDate);
                request.AddParameter("answerToPromiseSearch[0].index", "0");
                request.AddParameter("answerToPromiseSearch[0].disableDates", "");
                request.AddParameter("answerToPromiseSearch[0].dateOnlySelect", "true");
                request.AddParameter("answerToPromiseSearch[0].spAndPcView", "false");
                request.AddParameter("deliveries[0].deliveryDateSelect", data.DeliveryDateSelect);
                request.AddParameter("ordertrace", "96a3be3cf272e017046d1b2674a52bd3");
                request.AddParameter("order", "order");

                var response = await _restClient.ExecuteAsync(request);
                UpdateCookies(response);

                return response.Headers?.FirstOrDefault(h => h.Name == "Location")?.Value?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                if (retry < 10)
                {
                    await Task.Delay(GetRandom(200, 100));
                    return await CallPostConfirmAsync(data, cardCvv, userName, retry + 1);
                }
                throw;
            }
        }

        private async Task<(bool isSuccess, string html)> CallCompleteAsync(string nodeStateKey, int retry = 0)
        {
            var request = new RestRequest($"https://order.yodobashi.com/yc/order/complete/index.html?nodeStateKey={nodeStateKey}", Method.Get);

            request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Pragma", "no-cache");
            request.AddHeader("Referer", $"https://order.yodobashi.com/yc/order/confirm/index.html?nodeStateKey={nodeStateKey}");
            request.AddHeader("Sec-Fetch-Dest", "document");
            request.AddHeader("Sec-Fetch-Mode", "navigate");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("Sec-Fetch-User", "?1");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
            request.AddHeader("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("Cookie", GetCookiesString());

            var response = await _restClient.ExecuteAsync(request);

            if (string.IsNullOrEmpty(response.Content))
            {
                return (true, response.Content); // Assume success if no content
            }

            var isSuccess = !response.Content.Contains("大変申し訳ございません。お客様の操作が正常に完了しませんでした");
            return (isSuccess, response.Content);
        }

        private async Task ClearCartAsync()
        {
            try
            {
                var itemList = await GetCartItemAsync();
                while (!string.IsNullOrEmpty(itemList.DetailNo))
                {
                    await CallApiDeleteCartAsync(itemList.DetailNo, itemList.PostToken);
                    itemList = await GetCartItemAsync();
                }

                // Clear later buy items
                var detailNo = await CallApiLaterBuyAsync(itemList.PostToken);
                while (!string.IsNullOrEmpty(detailNo))
                {
                    await CallApiDeleteCartAsync(detailNo, itemList.PostToken, true);
                    detailNo = await CallApiLaterBuyAsync(itemList.PostToken);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue
            }
        }

        private async Task<CartItem> GetCartItemAsync(bool isLatest = false)
        {
            var request = new RestRequest("https://order.yodobashi.com/yc/shoppingcart/index.html", Method.Get);

            // Use real cookies instead of hardcoded ones
            AddStandardGetHeaders(request, "https://www.yodobashi.com/");

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            var doc = new HtmlDocument();
            doc.LoadHtml(response.Content);

            var name = isLatest ? "laterBuyOrdinaryProducts[0].detailNo" : "ordinaryProducts[0].detailNo";
            var detailNo = GetInputValue(doc, name);

            return new CartItem
            {
                DetailNo = string.IsNullOrEmpty(detailNo) ? null : detailNo,
                PostToken = doc.DocumentNode.SelectSingleNode("//input[@class='postToken']")?.GetAttributeValue("value", "")
            };
        }

        private async Task<string> CallApiLaterBuyAsync(string postToken)
        {
            try
            {
                var request = new RestRequest("https://order.yodobashi.com/yc/shoppingcart/ajax/leterBuy.html", Method.Post);

                request.AddHeader("Accept", "application/json, text/javascript, */*; q=0.01");
                request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
                request.AddHeader("Cache-Control", "no-cache");
                request.AddHeader("Connection", "keep-alive");
                request.AddHeader("Content-Type", "application/json;");
                request.AddHeader("Origin", "https://order.yodobashi.com");
                request.AddHeader("Pragma", "no-cache");
                request.AddHeader("Referer", "https://order.yodobashi.com/yc/shoppingcart/index.html?returnUrl=https%3A%2F%2Forder.yodobashi.com%2Fyc%2Fmypage%2Fmember%2Findex.html");
                request.AddHeader("Sec-Fetch-Dest", "empty");
                request.AddHeader("Sec-Fetch-Mode", "cors");
                request.AddHeader("Sec-Fetch-Site", "same-origin");
                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36");
                request.AddHeader("X-Requested-With", "XMLHttpRequest");
                request.AddHeader("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"137\", \"Chromium\";v=\"137\"");
                request.AddHeader("sec-ch-ua-mobile", "?0");
                request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
                request.AddHeader("Cookie", GetCookiesString());

                var data = new { postToken = postToken, pageIndex = "1" };
                request.AddJsonBody(data);

                var response = await _restClient.ExecuteAsync(request);
                UpdateCookies(response);

                var jsonResponse = JsonConvert.DeserializeObject<dynamic>(response.Content);
                var html = jsonResponse?.result?.data?.html?.ToString();

                if (!string.IsNullOrEmpty(html))
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return GetInputValue(doc, "laterBuyOrdinaryProducts[0].detailNo");
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private async Task CallApiDeleteCartAsync(string detailNo, string postToken, bool isLatest = false)
        {
            var request = new RestRequest("https://order.yodobashi.com/yc/shoppingcart/action.html", Method.Post);

            request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddHeader("Origin", "https://order.yodobashi.com");
            request.AddHeader("Pragma", "no-cache");
            request.AddHeader("Referer", "https://order.yodobashi.com/yc/shoppingcart/index.html?next=true");
            request.AddHeader("Sec-Fetch-Dest", "document");
            request.AddHeader("Sec-Fetch-Mode", "navigate");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("Sec-Fetch-User", "?1");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");
            request.AddHeader("sec-ch-ua", "\"Chromium\";v=\"134\", \"Not:A-Brand\";v=\"24\", \"Google Chrome\";v=\"134\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("Cookie", GetCookiesString());

            request.AddParameter("postToken", postToken);
            request.AddParameter("ordinaryProducts[0].detailNo", detailNo);
            request.AddParameter("ordinaryProducts[0].editable", "true");
            request.AddParameter("ordinaryProducts[0].amount", "1");
            request.AddParameter("detailNo", detailNo);
            request.AddParameter("deleteProduct", "deleteProduct");

            await _restClient.ExecuteAsync(request);
        }

        private async Task DeleteDefaultCardAsync()
        {
            try
            {
                var deleteUrl = await CallMemberIndexAsync();
                if (string.IsNullOrEmpty(deleteUrl)) return;

                var dataDelete = await CallCardDeletePageAsync(deleteUrl);
                await CallDeleteCardAsync(dataDelete, deleteUrl);
            }
            catch (Exception ex)
            {
                // Log error but continue
            }
        }

        private async Task<string> CallMemberIndexAsync()
        {
            var request = new RestRequest("https://order.yodobashi.com/yc/mypage/member/index.html", Method.Get);
            AddStandardGetHeaders(request, "https://order.yodobashi.com/yc/mypage/index.html");

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            var doc = new HtmlDocument();
            doc.LoadHtml(response.Content);

            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            foreach (var link in links ?? Enumerable.Empty<HtmlNode>())
            {
                var href = link.GetAttributeValue("href", "");
                if (href.Contains("&delete"))
                {
                    return $"https://order.yodobashi.com{href}";
                }
            }

            return "";
        }

        private async Task<CardDeleteData> CallCardDeletePageAsync(string url)
        {
            var request = new RestRequest(url, Method.Get);
            AddStandardGetHeaders(request, "https://order.yodobashi.com/yc/mypage/member/index.html");

            var response = await _restClient.ExecuteAsync(request);
            UpdateCookies(response);

            var doc = new HtmlDocument();
            doc.LoadHtml(response.Content);

            return new CardDeleteData
            {
                PostToken = GetInputValue(doc, "postToken"),
                NodeStateKey = GetInputValue(doc, "nodeStateKey")
            };
        }

        private async Task CallDeleteCardAsync(CardDeleteData data, string deleteUrl)
        {
            var request = new RestRequest("https://order.yodobashi.com/yc/mypage/card/delete.html", Method.Post);

            request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.AddHeader("Accept-Language", "en,en-US;q=0.9");
            request.AddHeader("Cache-Control", "max-age=0");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddHeader("Origin", "https://order.yodobashi.com");
            request.AddHeader("Referer", deleteUrl);
            request.AddHeader("Sec-Fetch-Dest", "document");
            request.AddHeader("Sec-Fetch-Mode", "navigate");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("Sec-Fetch-User", "?1");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");
            request.AddHeader("sec-ch-ua", "\"Chromium\";v=\"134\", \"Not:A-Brand\";v=\"24\", \"Google Chrome\";v=\"134\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("Cookie", GetCookiesString());

            request.AddParameter("postToken", data.PostToken);
            request.AddParameter("nodeStateKey", data.NodeStateKey);
            request.AddParameter("delete", "delete");

            await _restClient.ExecuteAsync(request);
        }

        private string GetInputValue(HtmlDocument doc, string name)
        {
            try
            {
                return doc.DocumentNode.SelectSingleNode($"//input[@name='{name}']")?.GetAttributeValue("value", "") ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string GetAkamaiScript(HtmlDocument doc)
        {
            var scripts = doc.DocumentNode.SelectNodes("//script[@src]");
            foreach (var script in scripts ?? Enumerable.Empty<HtmlNode>())
            {
                var src = script.GetAttributeValue("src", "");
                src = src.Replace("https://order.yodobashi.com", "").Replace("https://www.yodobashi.com", "");
                if (IsValidAkamaiScriptUrl(src))
                {
                    return script.GetAttributeValue("src", "");
                }
            }
            return "";
        }

        private bool IsValidAkamaiScriptUrl(string url)
        {
            var regex = new Regex(@"^\/([A-Za-z0-9_\-~]+\/){3,}[A-Za-z0-9_\-~]+$");
            return regex.IsMatch(url);
        }

        private FinalConfirmResult MergeConfirmAndDelivery(ConfirmResult confirm, DeliveryResult delivery)
        {
            return new FinalConfirmResult
            {
                PostToken = confirm.PostToken,
                NodeStateKey = confirm.NodeStateKey,
                AkamaiScriptUrl = confirm.AkamaiScriptUrl,
                DetailNo = delivery.DetailNo,
                DeliveryDateTypeSelect = delivery.DeliveryDateTypeSelect,
                ShortestDate = delivery.ShortestDate,
                StartTime = delivery.StartTime,
                EndTime = delivery.EndTime,
                DefaultDate = delivery.DefaultDate,
                DeliveryDateSelect = delivery.DeliveryDateSelect,
                DeliveryMethodSelect = delivery.DeliveryMethodSelect
            };
        }

        private void SaveHtmlLog(string userName, string htmlContent)
        {
            try
            {
                var today = DateTime.Now;
                var dateFolder = today.ToString("yyyyMMdd");
                var logDir = Path.Combine("result_html", dateFolder);

                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var fileName = $"{userName}.html";
                var filePath = Path.Combine(logDir, fileName);

                File.WriteAllText(filePath, htmlContent, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // Log error
            }
        }

        /// <summary>
        /// Add standard headers for GET requests to match JavaScript version
        /// </summary>
        private void AddStandardGetHeaders(RestRequest request, string referer = "https://order.yodobashi.com/")
        {
            request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
            request.AddHeader("Accept-Encoding", "gzip, deflate, br, zstd");
            request.AddHeader("Cache-Control", "no-cache");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Pragma", "no-cache");
            request.AddHeader("Referer", referer);
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
            request.AddHeader("Cookie", GetCookiesString());
            request.AddHeader("Sec-Fetch-Dest", "document");
            request.AddHeader("Sec-Fetch-Mode", "navigate");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("Sec-Fetch-User", "?1");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            request.AddHeader("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
        }

        /// <summary>
        /// Add standard headers for POST requests to match JavaScript version
        /// </summary>
        private void AddStandardPostHeaders(RestRequest request, string origin = "https://order.yodobashi.com", string referer = "https://order.yodobashi.com/")
        {
            request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.AddHeader("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
            request.AddHeader("Accept-Encoding", "gzip, deflate, br, zstd");
            request.AddHeader("Cache-Control", "max-age=0");
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddHeader("Origin", origin);
            request.AddHeader("Pragma", "no-cache");
            request.AddHeader("Referer", referer);
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
            request.AddHeader("Cookie", GetCookiesString());
            request.AddHeader("Sec-Fetch-Dest", "document");
            request.AddHeader("Sec-Fetch-Mode", "navigate");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("Sec-Fetch-User", "?1");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            request.AddHeader("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
        }

        #endregion

        #region Data Classes

        private class NextCartResult
        {
            public string PostToken { get; set; }
            public string DetailNo { get; set; }
            public string Editable { get; set; }
            public string Amount { get; set; }
        }

        private class PaymentResult
        {
            public string PostToken { get; set; }
            public string NodeStateKey { get; set; }
            public string Balance { get; set; }
            public string Total { get; set; }
            public string PointPaymentTypeCode { get; set; }
            public string PaymentTypeCode0 { get; set; }
        }

        private class ConfirmResult
        {
            public string PostToken { get; set; }
            public string NodeStateKey { get; set; }
            public string AkamaiScriptUrl { get; set; }
        }

        private class DeliveryResult
        {
            public string DetailNo { get; set; }
            public string DeliveryDateTypeSelect { get; set; }
            public string ShortestDate { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public string DefaultDate { get; set; }
            public string DeliveryDateSelect { get; set; }
            public string DeliveryMethodSelect { get; set; }
        }

        private class FinalConfirmResult
        {
            public string PostToken { get; set; }
            public string NodeStateKey { get; set; }
            public string AkamaiScriptUrl { get; set; }
            public string DetailNo { get; set; }
            public string DeliveryDateTypeSelect { get; set; }
            public string ShortestDate { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public string DefaultDate { get; set; }
            public string DeliveryDateSelect { get; set; }
            public string DeliveryMethodSelect { get; set; }
        }

        private class CartItem
        {
            public string DetailNo { get; set; }
            public string PostToken { get; set; }
        }

        private class CardDeleteData
        {
            public string PostToken { get; set; }
            public string NodeStateKey { get; set; }
        }

        private class Cookie
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Domain { get; set; }
            public string Path { get; set; }
        }

        #endregion
    }
}
