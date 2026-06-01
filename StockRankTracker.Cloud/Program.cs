using StockRankTracker.Cloud.Services;
using StockRankTracker.Cloud.Helpers;

var now = TimeZoneInfo.ConvertTimeFromUtc(
    DateTime.UtcNow,
    TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"));

Console.WriteLine($"[{now:yyyy-MM-dd HH:mm:ss}] 雲端爬蟲啟動");

// ===== 測試模式 =====
var isTestMode = Environment.GetEnvironmentVariable("TEST_MODE") == "true";
var forceDate = Environment.GetEnvironmentVariable("FORCE_DATE") ?? "";

if (isTestMode)
    Console.WriteLine("⚠️ 測試模式：跳過時間閘門");

// ===== 新增：時間閘門 =====
// 14 個允許的台北時間（分鐘）
int[] allowedMinutes = {
    9*60+1, 9*60+6, 9*60+10, 9*60+15,       // 09:01, 09:06, 09:10, 09:15
    9*60+30, 9*60+45,                          // 09:30, 09:45
    10*60, 10*60+30,                            // 10:00, 10:30
    11*60, 11*60+30,                            // 11:00, 11:30
    12*60, 12*60+30,                            // 12:00, 12:30
    13*60, 13*60+35                             // 13:00, 13:35
};

int nowMinutes = now.Hour * 60 + now.Minute;
int tolerance = 20;  // GitHub Actions 延遲容忍：±20 分鐘

var matched = allowedMinutes
    .Where(t => Math.Abs(nowMinutes - t) <= tolerance)
    .OrderBy(t => Math.Abs(nowMinutes - t))
    .FirstOrDefault(-1);

string timeTag;
if (isTestMode)
{
    timeTag = "1335";
    Console.WriteLine($"測試模式，timeTag 固定：{timeTag}");
}
else
{
    if (matched == -1)
    {
        Console.WriteLine($"目前時間 {now:HH:mm} 不在允許的 14 個時段內，跳過。");
        return;
    }
    timeTag = $"{matched / 60:D2}{matched % 60:D2}";
    Console.WriteLine($"匹配到目標時段：{timeTag}");
}
// ===== 時間閘門結束 =====

var holidays = DateTimeHelper.GetHolidays(now.Year);
if (!DateTimeHelper.IsTradingDay(now, holidays))
{
    Console.WriteLine("今天非交易日，跳過。");
    return;
}

Console.WriteLine("開始抓取 Yahoo 成交金額排行...");
var crawler = new TurnoverCrawler();
var stocks = await crawler.FetchAsync();
Console.WriteLine($"抓到 {stocks.Count} 筆資料");

if (stocks.Count == 0)
{
    Console.WriteLine("未抓到資料，結束。");
    return;
}

Console.WriteLine($"開始寫入 Firestore（快照 {timeTag}）...");
var firestore = new FirestoreRepository();
var todayStr = string.IsNullOrEmpty(forceDate) ? now.ToString("yyyy-MM-dd") : forceDate;
Console.WriteLine($"使用日期：{todayStr}");
await firestore.SaveDailyStocksAsync(todayStr, timeTag, stocks);  // ← 加 timeTag
Console.WriteLine("Firestore 寫入完成");

// ===== 新上榜比對：固定比對前一天 13:35 =====
Console.WriteLine("開始比對新上榜（對比前一天 13:35）...");
var previousDate = DateTimeHelper.GetPreviousTradingDate(now.Date);
var previousDateStr = previousDate.ToString("yyyy-MM-dd");
var detector = new NewEntryDetector(firestore);
var newEntries = await detector.DetectAsync(todayStr, timeTag, previousDateStr, stocks);
Console.WriteLine($"新上榜：{newEntries.Count} 檔");

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