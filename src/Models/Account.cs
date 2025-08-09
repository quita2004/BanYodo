using Newtonsoft.Json;

namespace BanYodo.Models
{
    public class Account
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Proxy { get; set; } = string.Empty;
        public string Card { get; set; } = string.Empty;
        public string CardYear { get; set; } = string.Empty;
        public string CardMonth { get; set; } = string.Empty;
        public string CardCvv { get; set; } = string.Empty;
        
        [JsonIgnore]
        public AccountStatus Status { get; set; } = AccountStatus.Ready;
        
        [JsonIgnore]
        public bool IsRunning { get; set; } = false;

        public string GetAccountInfo()
        {
            return $"{Username} {Password}";
        }

        public void SetAccountInfo(string accountInfo)
        {
            if (string.IsNullOrWhiteSpace(accountInfo))
                return;

            var parts = accountInfo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                Username = parts[0];
                Password = parts[1];
            }
        }

        public string GetCardInfo()
        {
            if (string.IsNullOrWhiteSpace(Card))
                return string.Empty;
            
            return $"{Card}/{CardMonth}/{CardYear}";
        }

        public bool IsValidCard()
        {
            return !string.IsNullOrWhiteSpace(Card) && 
                   !string.IsNullOrWhiteSpace(CardMonth) && 
                   !string.IsNullOrWhiteSpace(CardYear) && 
                   !string.IsNullOrWhiteSpace(CardCvv);
        }

        public bool IsValidAccount()
        {
            return !string.IsNullOrWhiteSpace(Username) && 
                   !string.IsNullOrWhiteSpace(Password);
        }
    }
}
