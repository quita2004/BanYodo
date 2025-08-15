namespace AutoPurchaseAPI.Models
{
    public class LicenseModel
    {
        public string LicenseKey { get; set; } = null!;
        public DateTime ExpiredAt { get; set; }
        public bool IsActive { get; set; }
    }
}
