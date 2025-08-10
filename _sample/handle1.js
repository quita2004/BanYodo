import puppeteer from 'puppeteer-extra';
import StealthPlugin from 'puppeteer-extra-plugin-stealth';
import ghostCursor from 'ghost-cursor';
import axios from 'axios';
import qs from 'qs';
import path from 'path';
import fs from 'fs';
import jsdom from 'jsdom';
import { URL } from 'url';
import { HttpsProxyAgent } from 'https-proxy-agent';

const { JSDOM } = jsdom;

const useAxiosProxy = true;

const headlessConst = true;

const TIME_CHECK_PRODUCT_AVAILABLE = 300;
const TIME_WAIT = 0.1;
let USE_OLD_CARD = true;

const NOT_ONLY_USE_PROXY_COMFIRM = true;

let cookies = [];
let axiosProxy = axios;
const args = process.argv;
console.log(args);

try {
    if (checkArgs()) {
        let success = false;
        success = await run();

        console.log(`success:${success}`);
    }
    process.exit(0);

} catch (error) {
    console.log(`error:${error}`);
    process.exit(1);
}

async function run(proxyParam = null, reTry = 0) {
    let userName = args[2]?.trim();
    let password = args[3]?.trim();
    let cardNumber = args[4]?.trim();
    let cardMonth = parseInt(args[5]?.trim());
    let cardYear = parseInt(args[6]?.trim());
    let cardCvv = args[7]?.trim();
    let productUrl = args[8]?.trim();
    let runTime = args[9]?.trim();
    let proxy = proxyParam === null ? args[10]?.trim() : proxyParam;
    let amount = parseInt(args[11]?.trim());
    let noSaveCard = args[12]?.trim();

    USE_OLD_CARD = noSaveCard !== 'noSaveCard';

    console.log(`start ${userName}: ${getCurrentTime()}`)
    puppeteer.use(StealthPlugin())

    var browserArgs = [
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-features=IsolateOrigins,site-per-process',
        '--ignore-certificate-errors'
    ];
    if (proxy !== 'noproxy') {
        let proxyUrl = '';
        if (countColons(proxy) > 2) {
            var splits = proxy.split(":");
            browserArgs.push(`--proxy-server=${splits[0]}:${splits[1]}`);

            proxyUrl = `http://${splits[2]}:${splits[3]}@${splits[0]}:${splits[1]}`;
        } else {
            browserArgs.push(`--proxy-server=${proxy}`);
            if (useAxiosProxy) {
                proxyUrl = `http://${proxy}`;
            }
        }
        let httpsAgent = new HttpsProxyAgent(proxyUrl);

        axiosProxy = axiosProxy.create({ httpsAgent });
        await getCurrentIp();
    }

    const browser = await puppeteer.launch({
        headless: headlessConst,
        args: browserArgs
    });
    const page = await browser.newPage()
    await page.setViewport(getRandomScreen());

    await sleep(3000);
    await page.setRequestInterception(true);
    page.setDefaultNavigationTimeout(60000);

    page.on('request', (request) => {
        if (request.resourceType() === 'stylesheet' ||
            request.resourceType() === 'font' ||
            request.resourceType() === 'image' && !request.url().includes('NoImage')) {
            request.abort();
        } else {
            request.continue();
        }
    })

    const cursor = ghostCursor.createCursor(page);

    // ghostCursor.installMouseHelper(page);

    if (countColons(proxy) > 2) {
        var splits = proxy.split(':');
        await page.authenticate({
            username: splits[2],
            password: splits[3]
        });
    }

    try {
        // vào trang chủ
        let resultGoYodo = await getGoYodoHome(page);
        if (!resultGoYodo) {
            await browser.close();
            console.log(`getGoYodoHome fail ${userName}: ${getCurrentTime()}`);
            if (reTry < 3) {
                await sleep(getRandom(200, 100));
                reTry++;
                return await run(proxy, reTry);
            } 
            return false;
        }
        // click trang đăng nhập
        await clickElement(page, cursor, '#logininfo a.cl-hdLO2_1');

        await cursor.click('#memberId', getDefaultOptionsClick());
        await page.locator('#memberId').fill(userName);
        await delayRandom();

        await cursor.move('#password', getDefaultOptionsMove());
        await page.locator('#password').fill(password);
        await delayRandom();

        await clickElement(page, cursor, '#js_i_login0');

        const loginfailMessage = !(await page.content()).includes("正しく入力されていない項目があります。メッセージをご確認の上、もう一度ご入力ください");
        if (!loginfailMessage) {
            console.log(`login fail ${userName}: ${getCurrentTime()}`);
            return false;
        }
        // login thành công
        console.log(`login success ${userName}: ${getCurrentTime()}`);
        // di chuyển chuột ngẫu nhiên
        // randomMove(page, cursor);

        cookies = await browser.cookies();

        await browser.close();
        // xóa giỏ hàng
        await clearCart();

        // delete default card
        if (!USE_OLD_CARD) {
            await deleteDefaultCard();
        }

        // Chờ đến thời gian mua hàng
        if (runTime !== 'rn') {
            const delay = getDelayUntil(runTime);
            await sleep(delay);
        }
        console.log(`start buy ${userName}: ${getCurrentTime()}`);
        // cookies = await browser.cookies();

        console.log(`start callApiAddCart: ${getCurrentTime()}`);

        let location = await callApiAddCart(productUrl, amount);
        console.log(`callApiAddCart location ${location}: ${getCurrentTime()}`);
        let countTryAddCard = 0;
        while (location.includes('error') && countTryAddCard < TIME_CHECK_PRODUCT_AVAILABLE) {
            countTryAddCard++;
            await sleep(TIME_WAIT * 1000);
            location = await callApiAddCart(productUrl, amount);
            console.log(`callApiAddCart location ${location}: ${getCurrentTime()}`);
        }

        sleepRandom();

        // console.log(`callPayment ${userName}: ${getCurrentTime()}`);
        // await callGetRecommend(location);
        // sleepRandom();

        console.log(`callNextCart ${userName}: ${getCurrentTime()}`);
        let resultCalNextCart = await callNextCart();
        sleepRandom();

        console.log(`callPayment ${userName}: ${getCurrentTime()}`);
        location = await callPayment(resultCalNextCart);
        console.log(`callPayment location ${location}: ${getCurrentTime()}`);
        sleepRandom();

        location = await callGetOrderIndex(location);
        sleepRandom();

        let nodeStateKey = getParamValue('nodeStateKey', location);

        let hasOldCard = false;
        let isReinputCredit = false;
        let postToken = '';
        if (location.includes('reinputcredit')) {
            isReinputCredit = true;
            // chọn thẻ thanh toán mặc định
            postToken = await getReinputIndex(nodeStateKey);
            console.log('postToken: ' + postToken)
            if (!USE_OLD_CARD) {
                location = await callReinputCredit(postToken, nodeStateKey);
            } else {
                location = await callReinputCredit(postToken, nodeStateKey, cardNumber, cardMonth, cardYear, 'true');
                location = await callOrderNext(location, nodeStateKey);
                hasOldCard = true;
            }
        } else if (location.includes('order/confirm/index.html')) {
            // đã có thẻ thanh toán, không cần nhập lại
            // chuyển sang bước nhập cvv
            hasOldCard = true;
        }

        if (!hasOldCard) {
            // https://order.yodobashi.com/yc/order/payment/index.html?nodeStateKey=xxx
            let resultPayment = await callGetpaymentIndex(location);
            postToken = resultPayment.postToken;
            sleepRandom();

            // Xác thực thẻ thanh toán
            cardNumber = await getPanToken(nodeStateKey, postToken, cardNumber);
            console.log('cardNumber: ' + cardNumber);

            console.log(`start callPostPayment ${userName}: ${getCurrentTime()}`);
            location = await callPostPayment(resultPayment, cardNumber, cardMonth, cardYear);
            sleepRandom();

            console.log(`start callPaymentNext: ${getCurrentTime()}`);
            location = await callPaymentNext(location, nodeStateKey);
            sleepRandom();
        }

        console.log(`start callGetConfirm ${userName}: ${getCurrentTime()}`);
        let resultConfirm = await callGetConfirm(location);

        console.log(`start callAkamaiScript ${userName} ${resultConfirm.akamaiScriptUrl}: ${getCurrentTime()}`);
        await callAkamaiScript(resultConfirm.akamaiScriptUrl, nodeStateKey);

        console.log(`start getDelivery : ${getCurrentTime()}`);
        let resultDelivery = await getDelivery(resultConfirm);
        sleepRandom();

        resultConfirm = { ...resultConfirm, ...resultDelivery };

        console.log(`start callPostConfirm ${userName}: ${getCurrentTime()}`);
        location = await callPostConfirm(resultConfirm, cardCvv, userName);
        sleepRandom();

        console.log(`start callComplete ${userName} ${location}: ${getCurrentTime()}`);
        let { isSuccess, html } = await callComplete(resultConfirm.nodeStateKey);
        if (!isSuccess) {
            console.log(`callComplete fail ${userName}: ${getCurrentTime()}`);
            saveHtmlLog(userName, html);
        }
        console.log(`buy success ${userName} ${isSuccess}: ${getCurrentTime()}`);
        return isSuccess;
    } catch (error) {
        console.log(`error run ${userName} ${getCurrentTime()}:\n${error}`);
        
        return false;
    }
}

function getDefaultOptionsMove() {
    return {
        paddingPercentage: getRandom(20),
        moveDelay: getRandom(500),
        randomizeMoveDelay: true,
        moveSpeed: getRandom(700, 400)
    };
}

function getDefaultOptionsClick() {
    return {
        hesitate: getRandom(50),
        randomizeMoveDelay: true,
        moveDelay: getRandom(500),
        moveSpeed: getRandom(700, 400)
    };
}
function checkArgs() {
    if (args.length < 9) {
        return false;
    }
    for (let index = 2; index < args.length; index++) {
        const element = args[index];
        if (!element) {
            return false;
        }
    }

    return true;
}

async function getGoYodoHome(page) {
    try {
        // vào trang chủ
        await page.goto('https://yodobashi.com');
        await delayRandom();

        return true;
    } catch (error) {
        console.log('error getGoYodoHome: ' + error.message);
    }
    return false;
}

async function sleepRandom() {
    await sleep(getRandom(20, 10));
}
async function randomMove(page, cursor) {
    setInterval(async () => {
        try {
            await delayRandom(5000); // Delay ngẫu nhiên từ 1 đến 6 giây
            // Di chuyển chuột đến vị trí ngẫu nhiên trong viewport
            const viewport = page.viewport();
            const x = Math.floor(Math.random() * viewport.width);
            const y = Math.floor(Math.random() * viewport.height);
            await cursor.moveTo({ x, y }, getDefaultOptionsClick());
        } catch (error) {
            console.log('error randomMove: ' + error)
        }
    }, 5000);
}

async function callAkamaiScript(url, nodeStateKey, count = 0) {
    try {

        console.log('callAkamaiScript: ' + url + ', count: ' + count);

        if (!url.startsWith('https://order.yodobashi.com')) {
            url = 'https://order.yodobashi.com' + url;
        }

        let config = {
            method: 'get',
            maxBodyLength: Infinity,
            url: url,
            headers: {
                'Accept': '*/*',
                'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
                'Cache-Control': 'no-cache',
                'Connection': 'keep-alive',
                'Pragma': 'no-cache',
                'Referer': 'https://order.yodobashi.com/yc/order/confirm/index.html?nodeStateKey=' + nodeStateKey,
                'Sec-Fetch-Dest': 'script',
                'Sec-Fetch-Mode': 'no-cors',
                'Sec-Fetch-Site': 'same-origin',
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
                'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
                'sec-ch-ua-mobile': '?0',
                'sec-ch-ua-platform': '"Windows"',
                'Cookie': getCookiesString(cookies)
            },
            validateStatus: function (status) {
                return status >= 200 && status < 300 || status === 404; // chấp nhận cả 404
            },
            maxRedirects: 0
        };

        let res = await axiosProxy.request(config);
        updateCookies(res);

        if (res.status === 404) {
            // Lỗi 404, response:
            /*
                <!DOCTYPE HTML PUBLIC "-//IETF//DTD HTML 2.0//EN">
                <html>
    
                <head>
                    <title>404 Not Found</title>
                </head>
    
                <body>
                    <h1>Not Found</h1>
                    <p>The requested URL /xHa0cesiWlGH4Ce0TFrI/uik7VpNttJNV3V/Mnh4HhYlVws/Zz/ZNeFAncjI1 was not found on this server.
                    </p>
                    <script type="text/javascript" src="/xHa0cesiWlGH4Ce0TFrI/uik7VpNttJNV3V/Mnh4HhYlVws/Zz/ZNeFAncjI"></script>
                </body>
    
                </html>
            */

            if (count > 5) {
                return;
            }

            var html = res.data;
            const dom = new JSDOM(html);
            let document = dom.window.document;

            let akamaiScriptUrl = getAkamaiScript(document);
            await callAkamaiScript(akamaiScriptUrl, nodeStateKey, count + 1);
            return;
        }
    } catch (error) {
        console.log('error callAkamaiScript: ' + url + error);
    }
}

function getAkamaiScript(document) {
    let scriptTags = document.getElementsByTagName('script');

    for (let index = 0; index < scriptTags.length; index++) {
        const element = scriptTags[index];

        let src = element.src.replace('https://order.yodobashi.com', '').replace('https://www.yodobashi.com', '');
        if (isValidAkamaiScriptUrl(src)) {
            return element.src;
        }
    }

    return '';
}

async function getDelivery(resultConfirm, reTry = 0) {
    try {
        let data = {
            "nodeStateKey": resultConfirm.nodeStateKey,
            "postToken": resultConfirm.postToken,
            "loadDemand": false
        };

        let config = {
            method: 'post',
            maxBodyLength: Infinity,
            url: 'https://order.yodobashi.com/yc/order/confirm/ajax/deliveryChange.html',
            headers: {
                'Accept': 'application/json, text/javascript, */*; q=0.01',
                'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
                'Cache-Control': 'no-cache',
                'Connection': 'keep-alive',
                'Content-Type': 'application/json;',
                'Origin': 'https://order.yodobashi.com',
                'Pragma': 'no-cache',
                'Referer': 'https://order.yodobashi.com/yc/order/confirm/index.html?nodeStateKey=' + resultConfirm.nodeStateKey,
                'Sec-Fetch-Dest': 'empty',
                'Sec-Fetch-Mode': 'cors',
                'Sec-Fetch-Site': 'same-origin',
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
                'X-Requested-With': 'XMLHttpRequest',
                'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
                'sec-ch-ua-mobile': '?0',
                'sec-ch-ua-platform': '"Windows"',
                'Cookie': getCookiesString(cookies)
            },
            data: data
        };

        let res = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
        updateCookies(res);

        let html = res.data.result.data.productListHtml;
        const dom = new JSDOM(html);
        let document = dom.window.document;

        let result = {};

        try {
            result.detailNo = getInputValue(document, 'originalProductDetails[0].detailNo');
            result.deliveryDateTypeSelect = getInputValue(document, 'deliveries[0].deliveryDateTypeSelect');
            result.shortestDate = getInputValue(document, 'deliveries[0].shortestDate');
            result.startTime = getInputValue(document, 'answerToPromiseSearch[0].startTime');
            result.endTime = getInputValue(document, 'answerToPromiseSearch[0].endTime');
            result.defaultDate = getInputValue(document, 'answerToPromiseSearch[0].defaultDate');
            result.deliveryDateSelect = getInputValue(document, 'deliveries[0].deliveryDateSelect');
            result.deliveryMethodSelect = getInputValue(document, 'deliveryMethodSelect');
        } catch (error) {
            console.log('error getDelivery: ' + res.data);
        }

        return result;
    } catch (error) {
        console.log('error getDelivery: ' + error);
        if (reTry < 10) {
            await sleep(getRandom(200, 100));
            return getDelivery(resultConfirm, reTry + 1);
        }
    }
}

function saveHtmlLog(userName, htmlContent) {
    try {
        const today = new Date();
        const dateFolder = today.getFullYear().toString() +
            (today.getMonth() + 1).toString().padStart(2, '0') +
            today.getDate().toString().padStart(2, '0');

        const logDir = path.join('result_html', dateFolder);

        // Create directory if it doesn't exist
        if (!fs.existsSync(logDir)) {
            fs.mkdirSync(logDir, { recursive: true });
        }

        const fileName = `${userName}.html`;
        const filePath = path.join(logDir, fileName);

        fs.writeFileSync(filePath, htmlContent, 'utf8');
        console.log(`HTML log saved: ${filePath}`);
    } catch (error) {
        console.log(`Error saving HTML log: ${error}`);
    }
}

async function callComplete(nodeStateKey, reTry = 0) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/order/complete/index.html?nodeStateKey=' + nodeStateKey,
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Pragma': 'no-cache',
            'Referer': 'https://order.yodobashi.com/yc/order/confirm/index.html?nodeStateKey=' + nodeStateKey,
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        }
    };

    let res = await axiosProxy.request(config);

    if (!res || !res.data) {
        console.log(`[${new Date().toISOString()}] callComplete failed: ${res.status}`);

        return { isSuccess: true, html: res.data }
        // if (reTry < 3) {
        //     await sleep(getRandom(2000, 1000));
        //     return callComplete(nodeStateKey, reTry + 1);
        // }

        // // nếu không có dữ liệu trả về, gọi history để kiểm tra trạng thái đơn hàng
        // console.log(`[${new Date().toISOString()}] callComplete no data, calling order history`);
        // let date = new Date();
        // return await callOrderhistory(date);
    }
    let isSuccess = !res.data.includes('大変申し訳ございません。お客様の操作が正常に完了しませんでした');
    return { isSuccess, html: res.data };
}

async function callOrderhistory(date) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/orderhistory/index.html',
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Pragma': 'no-cache',
            'Referer': 'https://order.yodobashi.com/yc/mypage/index.html',
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        }
    };

    let res = await axiosProxy.request(config);
    let dateText = `${date.getFullYear()}年${date.getMonth() + 1}月${date.getDate()}日`;
    let result = {
        isSuccess: false
    }
    if (!res.data.includes(dateText)) {
        return result;
    }

    result.isSuccess = true;

    return result;
}

async function callPostConfirm(data, cardCvv, reTry = 0, userName = '') {
    try {
        let dataPost = qs.stringify({
            'postToken': data.postToken,
            'nodeStateKey': data.nodeStateKey,
            'creditCard.paymentNumberIndex': '01',
            'creditCard.securityCode': cardCvv,
            '_receiptReceive': 'on',
            '_receiptMailReceive': 'on',
            'receiptName': '',
            'deliveryMethodSelect': data.deliveryMethodSelect,
            'y': data.deliveryMethodSelect,
            'originalProductDetails[0].selectedKeys': '',
            'originalProductDetails[0].detailNo': data.detailNo,
            'deliveries[0].deliveryDateTypeSelect': data.deliveryDateTypeSelect,
            'deliveries[0].shortestDate': data.shortestDate,
            'answerToPromiseSearch[0].startTime': data.startTime,
            'answerToPromiseSearch[0].endTime': data.endTime,
            'answerToPromiseSearch[0].defaultDate': data.defaultDate,
            'answerToPromiseSearch[0].index': '0',
            'answerToPromiseSearch[0].disableDates': '',
            'answerToPromiseSearch[0].dateOnlySelect': 'true',
            'answerToPromiseSearch[0].spAndPcView': 'false',
            'deliveries[0].deliveryDateSelect': data.deliveryDateSelect,
            'ordertrace': '96a3be3cf272e017046d1b2674a52bd3',
            'order': 'order'
        });

        let config = {
            method: 'post',
            maxBodyLength: Infinity,
            url: 'https://order.yodobashi.com/yc/order/confirm/action.html',
            headers: {
                'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
                'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
                'Cache-Control': 'no-cache',
                'Connection': 'keep-alive',
                'Content-Type': 'application/x-www-form-urlencoded',
                'Origin': 'https://order.yodobashi.com',
                'Pragma': 'no-cache',
                'Referer': 'https://order.yodobashi.com/yc/order/confirm/index.html?nodeStateKey=' + data.nodeStateKey,
                'Sec-Fetch-Dest': 'document',
                'Sec-Fetch-Mode': 'navigate',
                'Sec-Fetch-Site': 'same-origin',
                'Sec-Fetch-User': '?1',
                'Upgrade-Insecure-Requests': '1',
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
                'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
                'sec-ch-ua-mobile': '?0',
                'sec-ch-ua-platform': '"Windows"',
                'Cookie': getCookiesString(cookies)
            },
            data: dataPost,
            maxRedirects: 0,
            validateStatus: function (status) {
                return status >= 200 && status <= 302
            }
        };

        let result = await callAxiosRequest(config, true);
        updateCookies(result);
        if (!result || !result.headers || !result.headers.location) {
            // ghi log file html
            console.log(`[${new Date().toISOString()}] callPostConfirm failed: ${result.status}`);
            saveHtmlLog('callPostConfirm_' + userName, result.data);
        }
        return result.headers.location;
    } catch (error) {
        console.log(`[${new Date().toISOString()}] error callPostConfirm: ${error}`);
        if (reTry < 10) {
            await sleep(getRandom(200, 100));
            return callPostConfirm(data, cardCvv, reTry + 1);
        }
    }
}
async function callGetConfirm(url, reTry = 0) {
    try {
        let config = {
            method: 'get',
            maxBodyLength: Infinity,
            url: url,
            headers: {
                'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
                'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
                'Cache-Control': 'no-cache',
                'Connection': 'keep-alive',
                'Pragma': 'no-cache',
                'Referer': url,
                'Sec-Fetch-Dest': 'document',
                'Sec-Fetch-Mode': 'navigate',
                'Sec-Fetch-Site': 'same-origin',
                'Sec-Fetch-User': '?1',
                'Upgrade-Insecure-Requests': '1',
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
                'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
                'sec-ch-ua-mobile': '?0',
                'sec-ch-ua-platform': '"Windows"',
                'Cookie': getCookiesString(cookies)
            }
        };

        let res = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
        updateCookies(res);
        var html = res.data;
        const dom = new JSDOM(html);
        let document = dom.window.document;

        let akamaiScriptUrl = getAkamaiScript(document);

        let data = {};
        data.postToken = getInputValue(document, 'postToken');
        data.nodeStateKey = getInputValue(document, 'nodeStateKey');
        data.akamaiScriptUrl = akamaiScriptUrl;

        return data;
    } catch (error) {
        console.log('error callGetConfirm: ' + error);
        if (reTry < 10) {
            await sleep(getRandom(200, 100));
            return callGetConfirm(url, reTry + 1);
        }
    }
}

function getInputValue(document, name) {
    try {
        return document.getElementsByName(name)[0].value;
    } catch (error) {
        return '';
    }
}
async function callPaymentNext(url, nodeStateKey) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: url,
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Pragma': 'no-cache',
            'Referer': 'https://order.yodobashi.com/yc/order/payment/index.html?nodeStateKey=' + nodeStateKey,
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        },
        maxRedirects: 0,
        validateStatus: function (status) {
            return status >= 200 && status <= 302
        }
    };

    let result = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    updateCookies(result);
    return result.headers.location;
}

async function callOrderNext(url, nodeStateKey) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: url,
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Pragma': 'no-cache',
            'Referer': 'https://order.yodobashi.com/yc/order/reinputcredit/index.html?nodeStateKey=' + nodeStateKey,
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        },
        maxRedirects: 0,
        validateStatus: function (status) {
            return status >= 200 && status <= 302
        }
    };

    let result = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    updateCookies(result);
    return result.headers.location;
}

async function callPostPayment(resultPayment, cardNumber, cardMonth, cardYear) {
    console.log('paymentTypeCode0: ' + resultPayment.paymentTypeCode0)
    let isOtherCard = resultPayment.paymentTypeCode0.startsWith('CEN');
    let dataOrigin = {};
    if (isOtherCard) {
        dataOrigin = {
            '_alwaysUse': 'on',
            'next': 'next',
            'nodeStateKey': resultPayment.nodeStateKey,
            'paymentDetails[0].invalid': 'false',
            'paymentDetails[0].paymentTypeCode': resultPayment.paymentTypeCode0,
            'paymentDetails[1].cardNo': cardNumber,
            'paymentDetails[1].invalid': 'true',
            'paymentDetails[1].limitMonth': cardMonth,
            'paymentDetails[1].limitYear': cardYear,
            'paymentDetails[1].paymentTypeCode': '002',
            'paymentDetails[2].paymentTypeCode': '008',
            'paymentDetails[3].paymentTypeCode': '005',
            'paymentDetails[4].paymentTypeCode': '009',
            'paymentDetails[5].paymentTypeCode': '003',
            'paymentTypeCode': '002',
            'postToken': resultPayment.postToken,
            'balance': resultPayment.balance,
            'total': resultPayment.total,
            'pointPaymentTypeCode': resultPayment.pointPaymentTypeCode,
            'goldPointForUse': ''
        }
    } else {
        dataOrigin = {
            'postToken': resultPayment.postToken,
            'nodeStateKey': resultPayment.nodeStateKey,
            '_alwaysUse': 'on',
            'balance': resultPayment.balance,
            'total': resultPayment.total,
            'pointPaymentTypeCode': resultPayment.pointPaymentTypeCode,
            'goldPointForUse': '',
            'paymentTypeCode': '002',
            'paymentDetails[0].paymentTypeCode': '002',
            'paymentDetails[0].invalid': 'true',
            'paymentDetails[0].cardNo': cardNumber,
            'paymentDetails[0].limitMonth': cardMonth,
            'paymentDetails[0].limitYear': cardYear,
            'paymentDetails[1].paymentTypeCode': '008',
            'paymentDetails[2].paymentTypeCode': '005',
            'paymentDetails[3].paymentTypeCode': '009',
            'paymentDetails[4].paymentTypeCode': '003',
            'next': 'next'
        };
    }
    if (USE_OLD_CARD) {
        dataOrigin.alwaysUse = 'true';
    }

    let data = qs.stringify(dataOrigin);

    let config = {
        method: 'post',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/order/payment/action.html',
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Content-Type': 'application/x-www-form-urlencoded',
            'Origin': 'https://order.yodobashi.com',
            'Pragma': 'no-cache',
            'Referer': 'https://order.yodobashi.com/yc/order/payment/index.html?nodeStateKey=' + resultPayment.nodeStateKey,
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        },
        data: data,
        maxRedirects: 0,
        validateStatus: function (status) {
            return status >= 200 && status <= 302
        }
    };

    let result = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    updateCookies(result);
    return result.headers.location;
}

async function callReinputCredit(postToken, nodeStateKey, cardNo = '', limitMonth = ' ', limitYear = ' ', selectCredit = 'false') {
    let data = qs.stringify({
        'postToken': postToken,
        'nodeStateKey': nodeStateKey,
        'cardNo': cardNo,
        'limitMonth': limitMonth,
        'limitYear': limitYear,
        'selectCredit': selectCredit,
        'next': 'next'
    });

    let config = {
        method: 'post',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/order/reinputcredit/action.html',
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en,en-US;q=0.9',
            'Cache-Control': 'max-age=0',
            'Connection': 'keep-alive',
            'Content-Type': 'application/x-www-form-urlencoded',
            'Origin': 'https://order.yodobashi.com',
            'Referer': 'https://order.yodobashi.com/yc/order/reinputcredit/index.html?nodeStateKey=' + nodeStateKey,
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        },
        data: data,
        maxRedirects: 0,
        validateStatus: function (status) {
            return status >= 200 && status <= 302
        }
    };

    let res = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    updateCookies(res);
    return res.headers.location;
}

async function getReinputIndex(nodeStateKey) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/order/reinputcredit/index.html?nodeStateKey=' + nodeStateKey,
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en,en-US;q=0.9',
            'Connection': 'keep-alive',
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'none',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        }
    };

    let res = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    updateCookies(res);
    var html = res.data;
    const dom = new JSDOM(html);
    let document = dom.window.document;
    return getInputValue(document, 'postToken');
}

async function callGetpaymentIndex(url) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: url,
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Pragma': 'no-cache',
            'Referer': 'https://order.yodobashi.com/yc/shoppingcart/index.html?returnUrl=https%3A%2F%2Fwww.yodobashi.com%2F',
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        }
    };

    let res = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    updateCookies(res);
    var html = res.data;
    const dom = new JSDOM(html);
    let document = dom.window.document;

    let postToken = document.getElementsByName('postToken')[0].value;
    let nodeStateKey = document.getElementsByName('nodeStateKey')[0].value;
    return {
        postToken,
        nodeStateKey,
        balance: getInputValue(document, 'balance'),
        total: getInputValue(document, 'total'),
        pointPaymentTypeCode: getInputValue(document, 'pointPaymentTypeCode'),
        goldPointForUse: getInputValue(document, 'goldPointForUse'),
        paymentTypeCode: getInputValue(document, 'paymentTypeCode'),
        paymentTypeCode0: getInputValue(document, 'paymentDetails[0].paymentTypeCode')
    };
}

async function callGetOrderIndex(url) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: url,
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Pragma': 'no-cache',
            'Referer': 'https://order.yodobashi.com/yc/shoppingcart/index.html?returnUrl=https%3A%2F%2Fwww.yodobashi.com%2F',
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        },
        maxRedirects: 0,
        validateStatus: function (status) {
            return status >= 200 && status <= 302
        }
    };

    let result = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    updateCookies(result);
    return result.headers.location;
}

async function callPayment(resultCalNextCart, reTry = 0) {
    try {
        let data = qs.stringify({
            'postToken': resultCalNextCart.postToken,
            'ordinaryProducts[0].detailNo': resultCalNextCart.detailNo,
            'ordinaryProducts[0].editable': resultCalNextCart.editable,
            'ordinaryProducts[0].amount': resultCalNextCart.amount,
            'order': 'order'
        });

        let config = {
            method: 'post',
            maxBodyLength: Infinity,
            url: 'https://order.yodobashi.com/yc/shoppingcart/action.html',
            headers: {
                'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
                'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
                'Cache-Control': 'no-cache',
                'Connection': 'keep-alive',
                'Content-Type': 'application/x-www-form-urlencoded',
                'Origin': 'https://order.yodobashi.com',
                'Pragma': 'no-cache',
                'Referer': 'https://order.yodobashi.com/yc/shoppingcart/index.html?returnUrl=https%3A%2F%2Fwww.yodobashi.com%2F',
                'Sec-Fetch-Dest': 'document',
                'Sec-Fetch-Mode': 'navigate',
                'Sec-Fetch-Site': 'same-origin',
                'Sec-Fetch-User': '?1',
                'Upgrade-Insecure-Requests': '1',
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
                'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
                'sec-ch-ua-mobile': '?0',
                'sec-ch-ua-platform': '"Windows"',
                'Cookie': getCookiesString(cookies)
            },
            data: data,
            maxRedirects: 0,
            validateStatus: function (status) {
                return status >= 200 && status <= 302
            }
        };

        let result = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
        updateCookies(result);
        return result.headers.location;
    } catch (error) {
        console.log(`[${getCurrentTime()}] Error callPayment: ${JSON.stringify(error)}`);
        if (reTry < 10) {
            await sleep(getRandom(200, 100));
            console.log(`[${getCurrentTime()}] Retry callPayment ${reTry + 1}`);
            return await callPayment(resultCalNextCart, reTry + 1);
        }
        throw error;
    }
}

function getCookiesString(cookies) {
    let result = '';
    for (let index = 0; index < cookies.length; index++) {
        const element = cookies[index];
        result += `${element.name}=${element.value}`;
        if (index < cookies.length - 1) {
            result += ';';
        }
    }

    return result;
}

async function callNextCart(reTry = 0) {
    try {
        let config = {
            method: 'get',
            maxBodyLength: Infinity,
            url: 'https://order.yodobashi.com/yc/shoppingcart/index.html?next=true',
            headers: {
                'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
                'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
                'Cache-Control': 'no-cache',
                'Connection': 'keep-alive',
                'Pragma': 'no-cache',
                'Referer': 'https://order.yodobashi.com/yc/shoppingcart/recommend.html',
                'Sec-Fetch-Dest': 'document',
                'Sec-Fetch-Mode': 'navigate',
                'Sec-Fetch-Site': 'same-origin',
                'Sec-Fetch-User': '?1',
                'Upgrade-Insecure-Requests': '1',
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
                'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
                'sec-ch-ua-mobile': '?0',
                'sec-ch-ua-platform': '"Windows"',
                'Cookie': getCookiesString(cookies)
            }
        };

        let res = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
        updateCookies(res);
        var html = res.data;
        const dom = new JSDOM(html);
        let document = dom.window.document;

        let postToken = document.getElementsByName('postToken')[0].value;
        let detailNo = document.getElementsByName('ordinaryProducts[0].detailNo')[0].value;
        let editable = document.getElementsByName('ordinaryProducts[0].editable')[0].value;
        let amount = document.getElementsByName('ordinaryProducts[0].amount')[0].value;

        return {
            postToken,
            detailNo,
            editable,
            amount
        };
    } catch (error) {
        console.log(`[${getCurrentTime()}] Error callNextCart: ${JSON.stringify(error)}`);
        if (reTry < 10) {
            await sleep(getRandom(200, 100));
            console.log(`[${getCurrentTime()}] Retry callNextCart ${reTry + 1}`);
            return await callNextCart(reTry + 1);
        }
        throw error;
    }

}

async function callGetRecommend(url) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: url,
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Pragma': 'no-cache',
            'Referer': 'https://www.yodobashi.com/',
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-site',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        }
    };

    await axiosProxy.request(config);
}

function updateCookies(res) {
    let setCookies = res.headers['set-cookie'];
    for (let index = 0; index < setCookies.length; index++) {
        const element = setCookies[index];
        let content = element.split(';')[0];
        let keyValue = content.split('=');

        // value có dấu =
        if (keyValue.length > 2) {
            for (let i = 2; i < keyValue.length; i++) {
                keyValue[1] += '=' + keyValue[i];
            }
        }

        cookies = cookies.map(x => {
            if (x.name === keyValue[0]) {
                x.value = keyValue[1];
            }

            return x;
        });
        const exists = cookies.some(item => item.name === keyValue[0]);
        if (!exists) {
            cookies.push({
                key: keyValue[0],
                value: keyValue[1]
            });
        }
    }
}

function getSkuData(productId, amount = 1) {
    return {
        'postCode': '0791134',
        'returnUrl': `https://www.yodobashi.com/product/${productId}/index.html`,
        'products[0].cartInSKU': productId,
        'products[0].itemId': productId,
        'products[0].serviceFlag': '0',
        'products[0].amount': `${amount}`,
        'products[0].price': '0',
        'products[0].encryptPrice': 'ffeadb50e7afbc4e',
        'products[0].pointRate': '0',
        'products[0].encryptPointRate': '74ab67277c012bf7',
        'products[0].salesInformationCode': '0027',
        'products[0].salesReleaseDay': '2018/05/25',
        'products[0].salesReleaseDayString': '',
        'products[0].stockStatusCode': '0002',
        'products[0].isDownload': 'false',
        'products[0].readCheckFlg': '0'
    }

    // return {
    //     'postCode': '0791134',
    //     'returnUrl': `https://www.yodobashi.com/product/${productId}/index.html`,
    //     'products[0].cartInSKU': productId,
    //     'products[0].itemId': productId,
    //     'products[0].serviceFlag': '0',
    //     'products[0].amount': '1',
    //     'products[0].price': '0',
    //     'products[0].encryptPrice': '610f8d8efa6896f7',
    //     'products[0].pointRate': '0',
    //     'products[0].encryptPointRate': '74ab67277c012bf7',
    //     'products[0].salesInformationCode': '0002',
    //     'products[0].salesReleaseDay': '2016/05/24',
    //     'products[0].salesReleaseDayString': '',
    //     'products[0].stockStatusCode': '0001',
    //     'products[0].isDownload': 'false',
    //     'products[0].readCheckFlg': '0'
    // }
}

async function callApiAddCart(productId, amount) {
    try {
        let data = qs.stringify(getSkuData(productId, amount));

        let config = {
            method: 'post',
            maxBodyLength: Infinity,
            url: 'https://order.yodobashi.com/yc/shoppingcart/add/index.html',
            headers: {
                'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
                'Accept-Language': 'en-US,en;q=0.9,vi;q=0.8',
                'Cache-Control': 'no-cache',
                'Connection': 'keep-alive',
                'Content-Type': 'application/x-www-form-urlencoded',
                'Origin': 'https://www.yodobashi.com',
                'Pragma': 'no-cache',
                'Referer': 'https://www.yodobashi.com/',
                'Sec-Fetch-Dest': 'document',
                'Sec-Fetch-Mode': 'navigate',
                'Sec-Fetch-Site': 'same-site',
                'Sec-Fetch-User': '?1',
                'Upgrade-Insecure-Requests': '1',
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36',
                'sec-ch-ua': '"Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"',
                'sec-ch-ua-mobile': '?0',
                'sec-ch-ua-platform': '"Windows"',
                'Cookie': getCookiesString(cookies)
            },
            data: data,
            maxRedirects: 0,
            validateStatus: function (status) {
                return status >= 200 && status <= 302
            }
        };

        let response = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
        // Log status code and headers
        if (!response.headers.location) {
            console.log('No location header found in response: ', response.status);
            return 'error';
        }
        updateCookies(response)
        return response.headers.location;
    } catch (error) {
        console.log(error);
        return '';
    }
}

function getRandomScreen() {
    const screens = [{
        width: 1920,
        height: 1080
    },
    {
        width: 1280,
        height: 720
    },
    {
        width: 1334,
        height: 750
    },
    {
        width: 1024,
        height: 720
    },
    {
        width: 768,
        height: 600
    },
    {
        width: 1600,
        height: 1080
    }, {
        width: 1000,
        height: 800
    }, {
        width: 1400,
        height: 1080
    }];

    return screens[Math.floor(Math.random() * screens.length)];
}
function getRandom(max, min = 0) {
    return Math.random() * (max - min) + min;
}

async function delayRandom(milisecond = 10) {
    await sleep(getRandom(milisecond, 0));
}

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function clickElement(page, cursor, element, waitUntil = 'networkidle2') {
    await Promise.all([
        page.waitForNavigation({ waitUntil: waitUntil }), // networkidle2 domcontentloaded
        cursor.click(element, getDefaultOptionsClick()),
    ]);
    await delayRandom();
}

function getDelayUntil(timeString) {
    // Lấy thời gian hiện tại
    const now = new Date();

    // Tách giờ, phút, giây từ chuỗi HH:mm:ss
    const [hours, minutes, seconds] = timeString.split(":").map(Number);

    // Tạo đối tượng thời gian mục tiêu (trong hôm nay)
    const targetTime = new Date();
    targetTime.setHours(hours, minutes, seconds, 0);

    // Nếu thời gian đã trôi qua hôm nay, bỏ qua
    if (targetTime < now) {
        return -1;
    }

    // Tính khoảng thời gian cần chờ (ms)
    return targetTime - now;
}

function countColons(str) {
    return (str.match(/:/g) || []).length;
}

function getParamValue(paramName, url) {
    const urlObj = new URL(url);
    return urlObj.searchParams.get(paramName);
}

function isValidAkamaiScriptUrl(str) {
    // /t0XtfqUSi/5Kh/wA5/Itrxjo-q0l8M/3Xt9Vpz0VmQJcEf5/URFBPSwC/GQxlDW/sFPzY
    // /EvfOnaZOvVsdisP1ufvS2F4lZW0/N7p54fcXkwSm/DgFGMgE/Mz5/mGHJ3XVg
    const regex = /^\/([A-Za-z0-9_\-~]+\/){3,}[A-Za-z0-9_\-~]+$/;
    return regex.test(str);
}

async function clearCart() {
    try {
        let itemList = await getCartItem();
        while (itemList.detailNo !== null) {
            await callApiDeleteCart(itemList.detailNo, itemList.postToken);
            itemList = await getCartItem();
        }

        let detailNo = await callApiLeterBuy(itemList.postToken);
        while (detailNo) {
            await callApiDeleteCart(detailNo, itemList.postToken, true);
            detailNo = await callApiLeterBuy(itemList.postToken);
        }
    } catch (error) {
        console.log(error)
    }
}

async function getCartItem(isLastest = false) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/shoppingcart/index.html?returnUrl=https%3A%2F%2Fwww.yodobashi.com%2F',
        headers: {
            'host': 'order.yodobashi.com',
            'connection': 'keep-alive',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'upgrade-insecure-requests': '1',
            'user-agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'sec-fetch-site': 'same-site',
            'sec-fetch-mode': 'navigate',
            'sec-fetch-user': '?1',
            'sec-fetch-dest': 'document',
            'referer': 'https://www.yodobashi.com/',
            'accept-encoding': 'gzip, deflate, br, zstd',
            'accept-language': 'en-US,en;q=0.9',
            'cookie': getCookiesString(cookies)
        }
    };

    let res = await axiosProxy.request(config);
    updateCookies(res);
    var html = res.data;

    const dom = new JSDOM(html);
    let document = dom.window.document;
    let name = 'ordinaryProducts[0].detailNo';
    if (isLastest) {
        name = 'laterBuyOrdinaryProducts[0].detailNo';
    }
    let detailNo = getInputValue(document, name);

    if (detailNo === '') {
        return {
            detailNo: null,
            postToken: document.getElementsByClassName('postToken')[0].value
        };
    }

    return {
        detailNo: detailNo,
        postToken: document.getElementsByClassName('postToken')[0].value
    }
}

async function callApiLeterBuy(postToken) {
    try {
        let data = `{"postToken":"${postToken}","pageIndex":"1"}`;

        let config = {
            method: 'post',
            maxBodyLength: Infinity,
            url: 'https://order.yodobashi.com/yc/shoppingcart/ajax/leterBuy.html',
            headers: {
                'Accept': 'application/json, text/javascript, */*; q=0.01',
                'Accept-Language': 'en-US,en;q=0.9',
                'Cache-Control': 'no-cache',
                'Connection': 'keep-alive',
                'Content-Type': 'application/json;',
                'Origin': 'https://order.yodobashi.com',
                'Pragma': 'no-cache',
                'Referer': 'https://order.yodobashi.com/yc/shoppingcart/index.html?returnUrl=https%3A%2F%2Forder.yodobashi.com%2Fyc%2Fmypage%2Fmember%2Findex.html',
                'Sec-Fetch-Dest': 'empty',
                'Sec-Fetch-Mode': 'cors',
                'Sec-Fetch-Site': 'same-origin',
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36',
                'X-Requested-With': 'XMLHttpRequest',
                'sec-ch-ua': '"Google Chrome";v="137", "Chromium";v="137", "Not/A)Brand";v="24"',
                'sec-ch-ua-mobile': '?0',
                'sec-ch-ua-platform': '"Windows"',
                'Cookie': getCookiesString(cookies)
            },
            data: data
        };

        let res = await axiosProxy.request(config);
        updateCookies(res);

        var html = res.data.result.data.html;

        const dom = new JSDOM(html);
        let document = dom.window.document;

        return getInputValue(document, 'laterBuyOrdinaryProducts[0].detailNo');
    } catch (error) {
        console.log(`[${getCurrentTime()}] Error callApiLeterBuy: ${JSON.stringify(error)}`);
        return null;
    }
}

async function callApiDeleteCart(detailNo, postToken, isLastest = false) {
    let data = qs.stringify({
        'postToken': postToken,
        'ordinaryProducts[0].detailNo': detailNo,
        'ordinaryProducts[0].editable': 'true',
        'ordinaryProducts[0].amount': '1',
        'detailNo': detailNo,
        'deleteProduct': 'deleteProduct'
    });

    if (isLastest) {
        data = qs.stringify({
            'postToken': postToken,
            'ordinaryProducts[0].detailNo': detailNo,
            'ordinaryProducts[0].editable': 'true',
            'ordinaryProducts[0].amount': '1',
            'detailNo': detailNo,
            'deleteProduct': 'deleteProduct'
        });
    }

    let config = {
        method: 'post',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/shoppingcart/action.html',
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'en-US,en;q=0.9',
            'Cache-Control': 'max-age=0',
            'Connection': 'keep-alive',
            'Content-Type': 'application/x-www-form-urlencoded',
            'Origin': 'https://order.yodobashi.com',
            'Referer': 'https://order.yodobashi.com/yc/shoppingcart/index.html?next=true',
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        },
        data: data
    };

    await axiosProxy.request(config);
}

async function getPanToken(nodeStateKey, postToken, cardNumber) {
    try {
        let accessToken = await getAccessToken(nodeStateKey, postToken);
        let input = await postTokenize(accessToken, cardNumber);
        let panToken = await decryptPanToken(input, nodeStateKey);

        return panToken;
    } catch (error) {
        console.log(error);
        return cardNumber;
    }
}

async function getAccessToken(nodeStateKey, postToken) {
    let data = `{"nodeStateKey":"${nodeStateKey}","postToken":"${postToken}","displayId":"orderPayment"}`;

    let config = {
        method: 'post',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/ts/getAccessToken.html',
        headers: {
            'Accept': 'application/json, text/javascript, */*; q=0.01',
            'Accept-Language': 'en-US,en;q=0.9',
            'Connection': 'keep-alive',
            'Content-Type': 'application/json;',
            'Origin': 'https://order.yodobashi.com',
            'Referer': 'https://order.yodobashi.com/yc/order/payment/index.html?nodeStateKey=' + nodeStateKey,
            'Sec-Fetch-Dest': 'empty',
            'Sec-Fetch-Mode': 'cors',
            'Sec-Fetch-Site': 'same-origin',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'X-Requested-With': 'XMLHttpRequest',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        },
        data: data
    };

    let res = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    updateCookies(res);
    return {
        traceID: res.data.result.data.traceID,
        accessToken: res.data.result.data.accessToken,
        paramVerificationValue: res.data.result.data.paramVerificationValue
    };
}

async function postTokenize(input, cardNumber) {
    let data = `accessToken=${encodeURIComponent(input.accessToken)}&traceID=${encodeURIComponent(input.traceID)}&paramVerificationValue=${encodeURIComponent(input.paramVerificationValue)}&pan=${cardNumber}`;

    let config = {
        method: 'post',
        maxBodyLength: Infinity,
        url: 'https://tokenize.yodobashi.com/yc/credit/v1/Tokenize',
        headers: {
            'Accept': 'application/json, text/javascript, */*; q=0.01',
            'Accept-Language': 'en-US,en;q=0.9',
            'Connection': 'keep-alive',
            'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
            'Origin': 'https://order.yodobashi.com',
            'Referer': 'https://order.yodobashi.com/',
            'Sec-Fetch-Dest': 'empty',
            'Sec-Fetch-Mode': 'cors',
            'Sec-Fetch-Site': 'same-site',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"'
        },
        data: data
    };

    let res = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    return {
        accessToken: res.data.accessToken,
        etp: res.data.ETP,
        paramVerificationValue: res.data.paramVerificationValue,
    };
}

async function decryptPanToken(input, nodeStateKey) {
    let data = `{"etp":"${input.etp}","statusCodeDetail":[0],"accessToken":"${input.accessToken}","paramVerificationValue":"${input.paramVerificationValue}"}`;

    let config = {
        method: 'post',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/ts/decryptPanToken.html',
        headers: {
            'Accept': 'application/json, text/javascript, */*; q=0.01',
            'Accept-Language': 'en-US,en;q=0.9',
            'Connection': 'keep-alive',
            'Content-Type': 'application/json;',
            'Origin': 'https://order.yodobashi.com',
            'Referer': 'https://order.yodobashi.com/yc/order/payment/index.html?nodeStateKey=' + nodeStateKey,
            'Sec-Fetch-Dest': 'empty',
            'Sec-Fetch-Mode': 'cors',
            'Sec-Fetch-Site': 'same-origin',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'X-Requested-With': 'XMLHttpRequest',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        },
        data: data
    };

    let res = await callAxiosRequest(config, NOT_ONLY_USE_PROXY_COMFIRM);
    updateCookies(res);

    return res.data.result.data.panToken;
}

async function deleteDefaultCard() {
    try {
        let deleteUrl = await callMemberIndex();
        if (deleteUrl === '') return;

        let dataDelete = await callCardDeletePage(deleteUrl);
        dataDelete.deleteUrl = deleteUrl;

        await callDeleteCard(dataDelete);
    } catch (error) {
        console.log('deleteDefaultCard:\n' + error);
    }
}

async function callMemberIndex() {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/mypage/member/index.html',
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'vi,en-US;q=0.9,en;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Pragma': 'no-cache',
            'Referer': 'https://order.yodobashi.com/yc/mypage/index.html',
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        }
    };

    let res = await axiosProxy.request(config);

    updateCookies(res);
    var html = res.data;
    const dom = new JSDOM(html);
    let document = dom.window.document;

    let tagAs = document.getElementsByTagName('a');
    for (let index = 0; index < tagAs.length; index++) {
        const element = tagAs[index];
        if (element.href.includes('&delete')) {
            return `https://order.yodobashi.com${element.href}`;
        }
    }

    return '';
}

async function callCardDeletePage(url) {
    let config = {
        method: 'get',
        maxBodyLength: Infinity,
        url: url,
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'vi,en-US;q=0.9,en;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Pragma': 'no-cache',
            'Referer': 'https://order.yodobashi.com/yc/mypage/member/index.html',
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        }
    };

    let res = await axiosProxy.request(config);

    updateCookies(res);
    var html = res.data;
    const dom = new JSDOM(html);
    let document = dom.window.document;

    return {
        postToken: getInputValue(document, 'postToken'),
        nodeStateKey: getInputValue(document, 'nodeStateKey')
    };
}

async function callDeleteCard({ postToken, nodeStateKey, deleteUrl }) {
    let data = qs.stringify({
        'postToken': postToken,
        'nodeStateKey': nodeStateKey,
        'delete': 'delete'
    });

    let config = {
        method: 'post',
        maxBodyLength: Infinity,
        url: 'https://order.yodobashi.com/yc/mypage/card/delete.html',
        headers: {
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
            'Accept-Language': 'vi,en-US;q=0.9,en;q=0.8',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'Content-Type': 'application/x-www-form-urlencoded',
            'Origin': 'https://order.yodobashi.com',
            'Pragma': 'no-cache',
            'Referer': deleteUrl,
            'Sec-Fetch-Dest': 'document',
            'Sec-Fetch-Mode': 'navigate',
            'Sec-Fetch-Site': 'same-origin',
            'Sec-Fetch-User': '?1',
            'Upgrade-Insecure-Requests': '1',
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36',
            'sec-ch-ua': '"Chromium";v="134", "Not:A-Brand";v="24", "Google Chrome";v="134"',
            'sec-ch-ua-mobile': '?0',
            'sec-ch-ua-platform': '"Windows"',
            'Cookie': getCookiesString(cookies)
        },
        data: data
    };

    await axiosProxy.request(config);
}

async function getCurrentIp() {
    try {
        let config = {
            method: 'get',
            maxBodyLength: Infinity,
            url: 'https://api.ipify.org/?format=txt',
            headers: {}
        };

        let res = await axiosProxy.request(config);
        console.log(`----- IP: ${res.data}`);
    } catch (error) {
    }
}

function getCurrentTime() {
    const now = new Date();

    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const seconds = String(now.getSeconds()).padStart(2, '0');
    const milliseconds = String(now.getMilliseconds()).padStart(3, '0');

    return `${hours}:${minutes}:${seconds}.${milliseconds}`;
}

async function callAxiosRequest(config, useProxy = true) {
    return useProxy ? await axiosProxy.request(config) : axios.request(config);
}