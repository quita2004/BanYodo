namespace BanYodo.Models
{
    public enum AccountStatus
    {
        Ready,
        Running,
        Success,
        Failed,
        Stopped
    }

    public enum PurchaseMode
    {
        FixedTime,
        ScanMode
    }

    public enum Website
    {
        Yodobashi,
        Fujifilm
    }
}
