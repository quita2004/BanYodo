using BanYodo.Models;
using BanYodo.Strategies;
using PuppeteerSharp;

namespace BanYodo.Strategies
{
    public class FujifilmPurchaseStrategy : IPurchaseStrategy
    {
        public Website SupportedWebsite => Website.Fujifilm;

        public async Task<bool> CheckProductAvailabilityAsync(IPage page, string productId)
        {
            // Placeholder implementation for future development
            await Task.Delay(100);
            throw new NotImplementedException("Fujifilm purchase strategy is not yet implemented");
        }

        Task<(bool, FailedReason)> IPurchaseStrategy.LoginAsync(IPage page, IBrowser browser, Account account)
        {
            throw new NotImplementedException();
        }

        public Task<(bool, FailedReason)> PurchaseProductAsync(IPage page, List<string> productIds, Account account, Configuration configuration, int amount = 1)
        {
            throw new NotImplementedException();
        }

        bool IPurchaseStrategy.IsLoggedInAsync(IPage page)
        {
            throw new NotImplementedException();
        }
    }
}
