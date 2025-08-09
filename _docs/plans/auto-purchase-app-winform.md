# Kế hoạch phát triển ứng dụng WinForm mua hàng tự động

## Mục tiêu
Tạo ứng dụng WinForm .NET 8 cho phép người dùng tự động mua hàng trên các trang web thương mại điện tử với khả năng quản lý nhiều tài khoản, proxy và chế độ mua hàng khác nhau.

## Các bước thực hiện

### Bước 1: Thiết lập project cơ bản
- Tạo WinForm project .NET 8
- Cài đặt các package cần thiết:
  - TakasakiStudio.PuppeteerExtraSharp
  - Newtonsoft.Json (để lưu/đọc cấu hình)

### Bước 2: Thiết kế giao diện chính
- Form chính với các thành phần:
  - ComboBox chọn website (yodobashi.com, fujifilm.com - disable)
  - TextBox/ListBox để nhập danh sách ProductIds (dùng chung cho tất cả tài khoản)
  - DataGridView hiển thị danh sách tài khoản
  - Các nút điều khiển: Start All, Stop All, Add Account, Remove Account
  - Panel cấu hình chế độ mua hàng
  - StatusStrip hiển thị trạng thái

### Bước 3: Tạo model dữ liệu
- Account model: Username, Password, Proxy, Card, CardYear, CardMonth, CardCvv, Status, IsRunning
- PurchaseMode enum: FixedTime, ScanMode
- Configuration model để lưu cài đặt và productIds
- ProductIds: Danh sách sản phẩm dùng chung cho tất cả tài khoản

### Bước 4: Thiết kế DataGridView cho quản lý tài khoản
- Columns: Account Info, Proxy, Card Info (Card/Year/Month/CVV), Status, Actions (Start/Stop button)
- Cho phép edit inline
- Custom cell với button Start/Stop cho từng row

### Bước 5: Implement chức năng cơ bản
- Load/Save configuration từ file JSON (bao gồm accounts và productIds)
- Auto-load data khi khởi động ứng dụng
- Auto-save data khi có thay đổi
- Add/Remove accounts
- Validate account format (username password cách nhau bằng space)
- Validate proxy format và card information
- Quản lý danh sách ProductIds

### Bước 6: Tích hợp PuppeteerExtraSharp
- Tạo PuppeteerService class
- Implement browser launch với proxy support
- Navigate đến yodobashi.com
- Setup cho việc mở rộng các website khác

### Bước 7: Implement Purchase Logic Framework
- IPurchaseStrategy interface
- YodobashiPurchaseStrategy class (implement cơ bản)
- FujifilmPurchaseStrategy class (placeholder cho tương lai)
- PurchaseController để quản lý việc mua hàng

### Bước 8: Implement chế độ mua hàng
- Fixed Time Mode: Schedule mua hàng tại thời điểm cố định
- Scan Mode: Scan mỗi 5 giây để kiểm tra availability
- Task management cho multiple accounts

### Bước 9: Error handling và logging
- Try-catch cho các operation
- Log file để track activities
- User-friendly error messages

## APIs / UI / Models liên quan

### Models
```csharp
public class Account
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string Proxy { get; set; }
    public string Card { get; set; }
    public string CardYear { get; set; }
    public string CardMonth { get; set; }
    public string CardCvv { get; set; }
    public AccountStatus Status { get; set; }
    public bool IsRunning { get; set; }
}

public class Configuration
{
    public List<Account> Accounts { get; set; } = new();
    public List<string> ProductIds { get; set; } = new();
    public PurchaseMode PurchaseMode { get; set; }
    public Website SelectedWebsite { get; set; }
    public DateTime? FixedTime { get; set; }
}

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
```

### Services
- `PuppeteerService`: Quản lý browser instances
- `ConfigurationService`: Load/Save settings
- `PurchaseController`: Điều phối việc mua hàng
- `LoggingService`: Ghi log hoạt động

### UI Components
- `MainForm`: Form chính
- `AccountDataGridView`: Custom DataGridView
- `PurchaseModePanel`: Panel cấu hình chế độ mua
- `ProductIdsPanel`: Panel quản lý danh sách ProductIds
- `StatusPanel`: Hiển thị trạng thái tổng quan

### External Libraries
- TakasakiStudio.PuppeteerExtraSharp: Web automation
- Newtonsoft.Json: Configuration serialization
- System.Threading.Tasks: Async operations management

## Cấu trúc thư mục dự kiến
```
BanYodo/
└── src/
    ├── Models/
    │   ├── Account.cs
    │   ├── Configuration.cs
    │   └── Enums.cs
    ├── Services/
    │   ├── PuppeteerService.cs
    │   ├── ConfigurationService.cs
    │   ├── PurchaseController.cs
    │   └── LoggingService.cs
    ├── Strategies/
    │   ├── IPurchaseStrategy.cs
    │   ├── YodobashiPurchaseStrategy.cs
    │   └── FujifilmPurchaseStrategy.cs
    ├── Forms/
    │   ├── MainForm.cs
    │   └── Controls/
    │       ├── AccountDataGridView.cs
    │       ├── PurchaseModePanel.cs
    │       └── ProductIdsPanel.cs
    ├── Utils/
    │   └── Extensions.cs
    ├── Program.cs
    ├── BanYodo.csproj
    └── App_Data/
        └── (browser files)
```

## Ghi chú
- Ưu tiên implement Yodobashi.com trước
- Fujifilm.com sẽ được thêm vào sau
- Cần xem xét về legal compliance khi sử dụng automation
- Performance optimization cho việc chạy multiple browser instances
- Data persistence: Tự động lưu/tải cấu hình khi khởi động/thoát ứng dụng
- ProductIds được chia sẻ cho tất cả tài khoản để đảm bảo tính nhất quán
