# Phân tích quy trình mua hàng tự động Yodobashi.com - Chế độ thời gian cố định

## Tổng quan
File `handle1.js` là một script Node.js hoàn chỉnh thực hiện quy trình mua hàng tự động trên website Yodobashi.com cho chế độ mua vào thời gian cố định (Fixed Time Mode). Script sử dụng Puppeteer với các plugin stealth để mô phỏng hành vi người dùng thật và tránh phát hiện bot.

## Các thư viện và công nghệ sử dụng

### Core Libraries
- **puppeteer-extra**: Phiên bản mở rộng của Puppeteer với hỗ trợ plugin
- **puppeteer-extra-plugin-stealth**: Plugin ẩn dấu hiệu automation
- **ghost-cursor**: Mô phỏng chuyển động chuột tự nhiên
- **axios**: HTTP client cho các API calls
- **jsdom**: Parse và thao tác HTML DOM
- **https-proxy-agent**: Hỗ trợ proxy authentication

### Security Features
- **Stealth Plugin**: Ẩn dấu hiệu browser automation
- **Proxy Support**: Hỗ trợ proxy với authentication (format: host:port:user:pass)
- **Random Screen Resolution**: Ngẫu nhiên hóa kích thước màn hình
- **Natural Mouse Movement**: Chuyển động chuột tự nhiên với delay ngẫu nhiên

## Luồng xử lý chính (Main Flow)

### 1. Khởi tạo và Cấu hình
```javascript
// Input parameters từ command line arguments
userName, password, cardNumber, cardMonth, cardYear, cardCvv, 
productUrl, runTime, proxy, amount, noSaveCard
```

**Các thông số quan trọng:**
- `runTime`: Thời gian mua hàng (format: HH:mm:ss) hoặc 'rn' để mua ngay
- `proxy`: Proxy server (format: host:port:user:pass hoặc 'noproxy')
- `USE_OLD_CARD`: Flag để sử dụng thẻ đã lưu hay nhập thẻ mới

### 2. Browser Setup và Stealth Configuration
```javascript
puppeteer.use(StealthPlugin())
const browser = await puppeteer.launch({
    headless: headlessConst,
    args: browserArgs // Bao gồm proxy settings
});
```

**Browser Arguments:**
- `--no-sandbox`: Tắt sandbox cho môi trường server
- `--disable-setuid-sandbox`: Tắt setuid sandbox
- `--proxy-server`: Cấu hình proxy server
- `--ignore-certificate-errors`: Bỏ qua lỗi SSL

### 3. Request Interception & Optimization
```javascript
await page.setRequestInterception(true);
page.on('request', (request) => {
    // Chặn CSS, fonts, images (trừ NoImage) để tăng tốc độ
    if (request.resourceType() === 'stylesheet' || 
        request.resourceType() === 'font' ||
        request.resourceType() === 'image' && !request.url().includes('NoImage')) {
        request.abort();
    } else {
        request.continue();
    }
})
```

## Quy trình mua hàng chi tiết

### Phase 1: Authentication
1. **Truy cập trang chủ Yodobashi.com**
   ```javascript
   await page.goto('https://yodobashi.com');
   ```

2. **Điều hướng đến trang đăng nhập**
   ```javascript
   await clickElement(page, cursor, '#logininfo a.cl-hdLO2_1');
   ```

3. **Nhập thông tin đăng nhập**
   ```javascript
   await cursor.click('#memberId', getDefaultOptionsClick());
   await page.locator('#memberId').fill(userName);
   await cursor.move('#password', getDefaultOptionsMove());
   await page.locator('#password').fill(password);
   await clickElement(page, cursor, '#js_i_login0');
   ```

4. **Xác thực đăng nhập**
   ```javascript
   const loginfailMessage = !(await page.content()).includes("正しく入力されていない項目があります");
   ```

### Phase 2: Pre-purchase Preparation
1. **Lưu cookies và đóng browser**
   ```javascript
   cookies = await browser.cookies();
   await browser.close();
   ```

2. **Xóa giỏ hàng hiện tại**
   ```javascript
   await clearCart(); // Xóa tất cả sản phẩm trong giỏ
   ```

3. **Xóa thẻ thanh toán mặc định** (nếu cần)
   ```javascript
   if (!USE_OLD_CARD) {
       await deleteDefaultCard();
   }
   ```

### Phase 3: Timing & Execution
1. **Chờ đến thời gian mua hàng**
   ```javascript
   if (runTime !== 'rn') {
       const delay = getDelayUntil(runTime);
       await sleep(delay);
   }
   ```

2. **Thêm sản phẩm vào giỏ hàng**
   ```javascript
   let location = await callApiAddCart(productUrl, amount);
   // Retry logic để kiểm tra sản phẩm có sẵn
   while (location.includes('error') && countTryAddCard < TIME_CHECK_PRODUCT_AVAILABLE) {
       await sleep(TIME_WAIT * 1000);
       location = await callApiAddCart(productUrl, amount);
   }
   ```

### Phase 4: Checkout Process
1. **Tiến hành checkout**
   ```javascript
   let resultCalNextCart = await callNextCart();
   location = await callPayment(resultCalNextCart);
   location = await callGetOrderIndex(location);
   ```

2. **Xử lý thông tin thanh toán**
   - Kiểm tra có thẻ cũ hay cần nhập thẻ mới
   - Tokenize thông tin thẻ (security enhancement)
   - Xác thực thẻ thanh toán

3. **Xác nhận đơn hàng**
   ```javascript
   let resultConfirm = await callGetConfirm(location);
   await callAkamaiScript(resultConfirm.akamaiScriptUrl, nodeStateKey);
   let resultDelivery = await getDelivery(resultConfirm);
   location = await callPostConfirm(resultConfirm, cardCvv, userName);
   ```

4. **Hoàn tất đặt hàng**
   ```javascript
   let { isSuccess, html } = await callComplete(resultConfirm.nodeStateKey);
   ```

## Các kỹ thuật anti-detection quan trọng

### 1. Natural Mouse Movement
```javascript
function getDefaultOptionsMove() {
    return {
        paddingPercentage: getRandom(20),
        moveDelay: getRandom(500),
        randomizeMoveDelay: true,
        moveSpeed: getRandom(700, 400)
    };
}
```

### 2. Random Delays
```javascript
async function delayRandom(milisecond = 10) {
    await sleep(getRandom(milisecond, 0));
}
```

### 3. Random Screen Resolutions
```javascript
function getRandomScreen() {
    const screens = [
        {width: 1920, height: 1080},
        {width: 1280, height: 720},
        // ... more resolutions
    ];
    return screens[Math.floor(Math.random() * screens.length)];
}
```

### 4. Akamai Script Handling
```javascript
async function callAkamaiScript(url, nodeStateKey, count = 0) {
    // Xử lý Akamai anti-bot protection
    // Đệ quy để xử lý multiple redirects
}
```

## API Endpoints chính

### Cart Management
- `POST /yc/shoppingcart/add/index.html`: Thêm sản phẩm vào giỏ
- `GET /yc/shoppingcart/index.html`: Lấy thông tin giỏ hàng
- `POST /yc/shoppingcart/action.html`: Cập nhật/xóa sản phẩm

### Payment Processing
- `GET /yc/order/payment/index.html`: Trang thanh toán
- `POST /yc/order/payment/action.html`: Xử lý thông tin thanh toán
- `POST /yc/ts/getAccessToken.html`: Lấy access token cho tokenization
- `POST https://tokenize.yodobashi.com/yc/credit/v1/Tokenize`: Tokenize thông tin thẻ

### Order Confirmation
- `GET /yc/order/confirm/index.html`: Trang xác nhận đơn hàng
- `POST /yc/order/confirm/action.html`: Submit đơn hàng
- `GET /yc/order/complete/index.html`: Trang hoàn tất đơn hàng

## Error Handling & Retry Logic

### 1. Product Availability Check
```javascript
const TIME_CHECK_PRODUCT_AVAILABLE = 300; // 300 attempts
const TIME_WAIT = 0.1; // 0.1 second between attempts
```

### 2. Network Retry Pattern
```javascript
async function callApiWithRetry(apiFunction, maxRetries = 10) {
    for (let retry = 0; retry < maxRetries; retry++) {
        try {
            return await apiFunction();
        } catch (error) {
            if (retry < maxRetries - 1) {
                await sleep(getRandom(200, 100));
                continue;
            }
            throw error;
        }
    }
}
```

### 3. Cookie Management
```javascript
function updateCookies(res) {
    let setCookies = res.headers['set-cookie'];
    // Cập nhật cookies từ response headers
    // Xử lý cookies với format phức tạp
}
```

## Logging & Debugging

### 1. HTML Error Logging
```javascript
function saveHtmlLog(userName, htmlContent) {
    const today = new Date();
    const dateFolder = today.getFullYear().toString() + 
                      (today.getMonth() + 1).toString().padStart(2, '0') + 
                      today.getDate().toString().padStart(2, '0');
    const logDir = path.join('result_html', dateFolder);
    // Lưu HTML response khi có lỗi
}
```

### 2. Timestamp Logging
```javascript
function getCurrentTime() {
    const now = new Date();
    return `${hours}:${minutes}:${seconds}.${milliseconds}`;
}
```

## Security Considerations

### 1. Card Information Tokenization
- Sử dụng Yodobashi tokenization service
- Không lưu trữ thông tin thẻ raw
- Access token và encryption cho bảo mật

### 2. Proxy Support
- Hỗ trợ authenticated proxy
- IP rotation capability
- Bypass geo-restrictions

### 3. Rate Limiting
- Random delays giữa các requests
- Respect server response times
- Avoid overwhelming target server

## Optimization Strategies

### 1. Resource Blocking
- Chặn CSS, fonts, images không cần thiết
- Giảm bandwidth và tăng tốc độ load

### 2. Parallel Processing
- Sử dụng axios cho HTTP requests
- Cookie-based session management
- Minimize browser overhead

### 3. Memory Management
- Đóng browser sau authentication
- Sử dụng HTTP requests cho majority operations
- Only use browser khi cần thiết

## Kết luận

Script `handle1.js` là một implementation hoàn chỉnh và sophisticated cho việc mua hàng tự động trên Yodobashi.com. Nó sử dụng combination của browser automation và HTTP requests để đạt được hiệu suất tối ưu và tránh detection. 

**Điểm mạnh:**
- Stealth capabilities cao
- Robust error handling
- Natural user behavior simulation
- Comprehensive logging
- Security-focused approach

**Considerations cho C# implementation:**
- Cần port các anti-detection techniques
- Implement tương tự retry logic
- Maintain cookie management strategy
- Preserve timing và delay patterns
- Adapt tokenization flow cho .NET environment
