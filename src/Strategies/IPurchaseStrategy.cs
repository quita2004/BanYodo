using BanYodo.Models;
using PuppeteerSharp;

namespace BanYodo.Strategies
{
    public interface IPurchaseStrategy
    {
        Task<(bool, FailedReason)> LoginAsync(IPage page, IBrowser browser, Account account);
        Task<bool> CheckProductAvailabilityAsync(IPage page, string productId);
        Task<(bool, FailedReason)> PurchaseProductAsync(IPage page, List<string> productIds, Account account, Configuration configuration, int amount = 1);
        bool IsLoggedInAsync(IPage page);
        Website SupportedWebsite { get; }
    }
}
