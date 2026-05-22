using StockRankTracker.Helpers;
using StockRankTracker.Models.Crawlers;
using StockRankTracker.Models.Entities;

namespace StockRankTracker.Tests;

/// <summary>
/// 第四步測試：驗證三個 Helper 是否正常運作
/// 在 App.xaml.cs 的 OnStartup 裡呼叫 HelperTest.RunAsync()
/// </summary>
public static class HelperTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine("===== Helper 測試開始 =====\n");

        TestDateTimeHelper();
        TestStockCalculateHelper();
        TestDatabaseHelper();

        Console.WriteLine("\n===== Helper 測試完成 =====");
        await Task.CompletedTask;
    }

    static void TestDateTimeHelper()
    {
        Console.WriteLine("[DateTimeHelper 測試]");

        // 盤中時間判斷
        var morning = new DateTime(2026, 4, 27, 9, 30, 0);   // 週一 09:30
        var noon = new DateTime(2026, 4, 27, 12, 0, 0);      // 週一 12:00
        var afterClose = new DateTime(2026, 4, 27, 14, 0, 0); // 週一 14:00
        var closeTime = new DateTime(2026, 4, 27, 13, 35, 0); // 週一 13:35

        Console.WriteLine($"  09:30 是盤中？ {DateTimeHelper.IsMarketHours(morning)} (應為 True)");
        Console.WriteLine($"  12:00 是盤中？ {DateTimeHelper.IsMarketHours(noon)} (應為 True)");
        Console.WriteLine($"  14:00 是盤中？ {DateTimeHelper.IsMarketHours(afterClose)} (應為 False)");
        Console.WriteLine($"  13:35 是收盤？ {DateTimeHelper.IsCloseTime(closeTime)} (應為 True)");

        // 假日判斷
        var saturday = new DateTime(2026, 4, 25);  // 週六
        var sunday = new DateTime(2026, 4, 26);    // 週日
        var monday = new DateTime(2026, 4, 27);    // 週一

        Console.WriteLine($"  週六是假日？ {DateTimeHelper.IsWeekend(saturday)} (應為 True)");
        Console.WriteLine($"  週日是假日？ {DateTimeHelper.IsWeekend(sunday)} (應為 True)");
        Console.WriteLine($"  週一是假日？ {DateTimeHelper.IsWeekend(monday)} (應為 False)");

        // 國定假日
        var holidays = DateTimeHelper.GetHolidays(2026);
        var newYear = new DateTime(2026, 1, 1);
        Console.WriteLine($"  2026/1/1 是國定假日？ {DateTimeHelper.IsHoliday(newYear, holidays)} (應為 True)");
        Console.WriteLine($"  2026/4/27 是國定假日？ {DateTimeHelper.IsHoliday(monday, holidays)} (應為 False)");

        // 交易日
        Console.WriteLine($"  週一是交易日？ {DateTimeHelper.IsTradingDay(monday, holidays)} (應為 True)");
        Console.WriteLine($"  週六是交易日？ {DateTimeHelper.IsTradingDay(saturday, holidays)} (應為 False)");
        Console.WriteLine($"  元旦是交易日？ {DateTimeHelper.IsTradingDay(newYear, holidays)} (應為 False)");

        Console.WriteLine("✓ DateTimeHelper 測試完成\n");
    }

    static void TestStockCalculateHelper()
    {
        Console.WriteLine("[StockCalculateHelper 測試]");

        // 模擬昨天收盤前30名
        var yesterdayClose = new List<DailyCloseEntity>
        {
            new() { Rank = 1, Symbol = "2330", Name = "台積電" },
            new() { Rank = 2, Symbol = "2454", Name = "聯發科" },
            new() { Rank = 3, Symbol = "2317", Name = "鴻海" },
            new() { Rank = 5, Symbol = "2308", Name = "台達電" },
            new() { Rank = 10, Symbol = "3105", Name = "穩懋" },
            new() { Rank = 25, Symbol = "2408", Name = "南亞科" },
            new() { Rank = 30, Symbol = "3189", Name = "景碩" },
        };

        // 測試名次變化
        // 台積電：昨天第1，今天第1 → 變化 0
        var change1 = StockCalculateHelper.CalcRankChange(1, "2330", yesterdayClose);
        Console.WriteLine($"  台積電 昨1→今1：變化={change1} (應為 0)");

        // 聯發科：昨天第2，今天第5 → 變化 -3（下降）
        var change2 = StockCalculateHelper.CalcRankChange(5, "2454", yesterdayClose);
        Console.WriteLine($"  聯發科 昨2→今5：變化={change2} (應為 -3)");

        // 穩懋：昨天第10，今天第3 → 變化 +7（上升）
        var change3 = StockCalculateHelper.CalcRankChange(3, "3105", yesterdayClose);
        Console.WriteLine($"  穩懋   昨10→今3：變化={change3} (應為 7)");

        // 全漢：昨天不在名單 → null（新上榜）
        var change4 = StockCalculateHelper.CalcRankChange(1, "3015", yesterdayClose);
        Console.WriteLine($"  全漢   不在昨天：變化={change4 ?.ToString() ?? "null"} (應為 null)");

        // 測試新上榜判斷
        // 全漢今天第1，昨天不在前30 → 新上榜
        var isNew1 = StockCalculateHelper.IsNewEntry(1, "3015", yesterdayClose);
        Console.WriteLine($"  全漢 今天第1名 新上榜？ {isNew1} (應為 True)");

        // 台積電今天第1，昨天也在前30 → 不是新上榜
        var isNew2 = StockCalculateHelper.IsNewEntry(1, "2330", yesterdayClose);
        Console.WriteLine($"  台積電 今天第1名 新上榜？ {isNew2} (應為 False)");

        // 測試統計計算
        var testStocks = new List<StockEntry>
        {
            new() { Market = "TW",  Change = "+3.6" },
            new() { Market = "TW",  Change = "-2.1" },
            new() { Market = "TWO", Change = "0" },
            new() { Market = "TW",  Change = "+15.0" },
            new() { Market = "TWO", Change = "-0.5" },
        };

        var stats = StockCalculateHelper.CalcStatistics(testStocks);
        Console.WriteLine($"  統計：上市={stats.ListedCount}(應3) 上櫃={stats.OtcCount}(應2) " +
                          $"上漲={stats.UpCount}(應2) 平盤={stats.FlatCount}(應1) 下跌={stats.DownCount}(應2)");

        Console.WriteLine("✓ StockCalculateHelper 測試完成\n");
    }

    static void TestDatabaseHelper()
    {
        Console.WriteLine("[DatabaseHelper 測試]");

        // 測試資料變化比對
        var current = new List<StockEntry>
        {
            new() { Rank = 1, Symbol = "2330", Price = "905.0" },
            new() { Rank = 2, Symbol = "2454", Price = "1185.0" },
        };

        // 與 null 比 → 有變化
        var changed1 = DatabaseHelper.IsDataChanged(current, null);
        Console.WriteLine($"  與 null 比：有變化？ {changed1} (應為 True)");

        // 與相同資料比 → 沒變化
        var same = new List<IntradaySnapshotEntity>
        {
            new() { Rank = 1, Symbol = "2330", Price = "905.0" },
            new() { Rank = 2, Symbol = "2454", Price = "1185.0" },
        };
        var changed2 = DatabaseHelper.IsDataChanged(current, same);
        Console.WriteLine($"  與相同資料比：有變化？ {changed2} (應為 False)");

        // 與不同資料比 → 有變化
        var different = new List<IntradaySnapshotEntity>
        {
            new() { Rank = 1, Symbol = "2330", Price = "900.0" },  // 價格不同
            new() { Rank = 2, Symbol = "2454", Price = "1185.0" },
        };
        var changed3 = DatabaseHelper.IsDataChanged(current, different);
        Console.WriteLine($"  與不同價格比：有變化？ {changed3} (應為 True)");

        // 測試轉換
        var stocks = new List<StockEntry>
        {
            new()
            {
                Rank = 1, Symbol = "2330", Name = "台積電", Market = "TW",
                Open = "895.0", Price = "905.0", Change = "+15.0", ChangePercent = "+1.7%",
                DayHigh = "910.0", DayLow = "895.0", DayHighLowDiff = "15.0",
                VolK = "35000", TurnoverHundredMillion = "85.00"
            }
        };

        var dailyEntities = DatabaseHelper.ToDailyCloseEntities(stocks, DateTime.Today, DateTime.Now);
        Console.WriteLine($"  轉換 DailyClose：{dailyEntities.Count} 筆，代號={dailyEntities[0].Symbol} (應為 2330)");

        var snapshotEntities = DatabaseHelper.ToIntradaySnapshotEntities(stocks, 1);
        Console.WriteLine($"  轉換 Snapshot：{snapshotEntities.Count} 筆，SessionId={snapshotEntities[0].SessionId} (應為 1)");

        Console.WriteLine("✓ DatabaseHelper 測試完成");
    }
}
