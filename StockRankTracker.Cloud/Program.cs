using StockRankTracker.Cloud.Services;
using StockRankTracker.Cloud.Helpers;

// ===== 時區與交易日判斷 =====
var now = TimeZoneInfo.ConvertTimeFromUtc(
    DateTime.UtcNow,
    TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"));

Console.WriteLine($"[{now:yyyy-MM-dd HH:mm:ss}] 雲端爬蟲啟動");

var holidays = DateTimeHelper.GetHolidays(now.Year);
if (!DateTimeHelper.IsTradingDay(now, holidays))
{
    Console.WriteLine("今天非交易日，跳過。");
    return;
}

// ===== 1. 爬蟲抓資料 =====
Console.WriteLine("開始抓取 Yahoo 成交金額排行...");
var crawler = new TurnoverCrawler();
var stocks = await crawler.FetchAsync();
Console.WriteLine($"抓到 {stocks.Count} 筆資料");

if (stocks.Count == 0)
{
    Console.WriteLine("未抓到資料，結束。");
    return;
}

// ===== 2. 存 Firestore =====
Console.WriteLine("開始寫入 Firestore...");
var firestore = new FirestoreRepository();
var todayStr = now.ToString("yyyy-MM-dd");
await firestore.SaveDailyStocksAsync(todayStr, stocks);
Console.WriteLine("Firestore 寫入完成");

// ===== 3. 比對新上榜 =====
Console.WriteLine("開始比對新上榜...");
var previousDate = DateTimeHelper.GetPreviousTradingDate(now.Date);
var previousDateStr = previousDate.ToString("yyyy-MM-dd");
var detector = new NewEntryDetector(firestore);
var newEntries = await detector.DetectAsync(todayStr, previousDateStr, stocks);
Console.WriteLine($"新上榜：{newEntries.Count} 檔");

//// ===== 測試 Telegram（測完後刪掉這段）=====
//var testEntries = stocks.Take(3).ToList();
//var testNotifier = new TelegramNotifier();
//await testNotifier.SendAsync(now, testEntries);
//Console.WriteLine("✅ 測試 Telegram 已送出");
//// ===== 測試結束 =====

// ===== 4. Telegram 推播 =====
if (newEntries.Count > 0)
{
    Console.WriteLine("發送 Telegram 通知...");
    var notifier = new TelegramNotifier();
    await notifier.SendAsync(now, newEntries);
    Console.WriteLine("Telegram 通知已送出");
}
else
{
    Console.WriteLine("無新上榜股票，不推播。");
}

Console.WriteLine("完成！");