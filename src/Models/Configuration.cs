using Newtonsoft.Json;

namespace BanYodo.Models
{
    public class Configuration
    {
        public List<Account> Accounts { get; set; } = new();
        public List<string> ProductIds { get; set; } = new();
        public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.ScanMode;
        public Website SelectedWebsite { get; set; } = Website.Yodobashi;
        public DateTime? FixedTime { get; set; }

        public void AddAccount(Account account)
        {
            if (account != null && account.IsValidAccount())
            {
                Accounts.Add(account);
            }
        }

        public void RemoveAccount(Account account)
        {
            if (account != null)
            {
                Accounts.Remove(account);
            }
        }

        public void ClearAccounts()
        {
            Accounts.Clear();
        }

        public void AddProductId(string productId)
        {
            if (!string.IsNullOrWhiteSpace(productId) && !ProductIds.Contains(productId))
            {
                ProductIds.Add(productId.Trim());
            }
        }

        public void RemoveProductId(string productId)
        {
            if (!string.IsNullOrWhiteSpace(productId))
            {
                ProductIds.Remove(productId.Trim());
            }
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static Configuration FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<Configuration>(json) ?? new Configuration();
            }
            catch
            {
                return new Configuration();
            }
        }
    }
}
