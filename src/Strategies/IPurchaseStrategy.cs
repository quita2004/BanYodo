using BanYodo.Models;
using PuppeteerSharp;

namespace BanYodo.Strategies
{
    public interface IPurchaseStrategy
    {
        Task<bool> LoginAsync(IPage page, Account account);
        Task<bool> CheckProductAvailabilityAsync(IPage page, string productId);
        Task<bool> PurchaseProductAsync(IPage page, string productId, Account account);
        Task<bool> IsLoggedInAsync(IPage page);
        Website SupportedWebsite { get; }
    }
}
