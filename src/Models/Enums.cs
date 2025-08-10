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

    public enum FailedReason
    {
        None,
        ProxyError,
        PasswordIncorrect,
        UnknownError,
        AddCartFailed,
        OutOfStock
    }
}
