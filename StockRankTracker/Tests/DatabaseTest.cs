using StockRankTracker.Data;
using StockRankTracker.Data.Repositories;
using StockRankTracker.Models.Entities;

namespace StockRankTracker.Tests;

/// <summary>
/// 第二步測試：驗證資料庫層是否正常運作
/// 在 App.xaml.cs 的 OnStartup 裡呼叫 DatabaseTest.RunAsync()
/// 測試完成後記得移除
/// </summary>
public static class DatabaseTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine("===== 資料庫測試開始 =====\n");

        using var db = new DatabaseContext();

        // 1. 建立資料庫（如果不存在）
        await db.Database.EnsureCreatedAsync();
        Console.WriteLine(" 資料庫建立成功");
        Console.WriteLine($"  路徑：{db.GetDatabasePath()}\n");

        var repo = new StockRepository(db);

        // 2. 測試寫入收盤快照
        var testClose = new List<DailyCloseEntity>
        {
            new()
            {
                RankType = "Turnover",
                SnapshotDate = DateTime.Today,
                FetchTime = DateTime.Now,
                Rank = 1,
                Symbol = "2330",
                Name = "台積電",
                Market = "TW",
                Open = "",
                Price = "905.0",
                Change = "15.0",
                ChangePercent = "1.7%",
                DayHigh = "910.0",
                DayLow = "895.0",
                DayHighLowDiff = "15.0",
                VolK = "35000",
                TurnoverHundredMillion = "85.00"
            },
            new()
            {
                RankType = "Turnover",
                SnapshotDate = DateTime.Today,
                FetchTime = DateTime.Now,
                Rank = 2,
                Symbol = "3015",
                Name = "全漢",
                Market = "TW",
                Open = "",
                Price = "64.7",
                Change = "3.6",
                ChangePercent = "5.9%",
                DayHigh = "65.0",
                DayLow = "61.0",
                DayHighLowDiff = "4.0",
                VolK = "12000",
                TurnoverHundredMillion = "3.40"
            }
        };

        await repo.SaveDailyCloseAsync(testClose);
        Console.WriteLine(" 收盤快照寫入成功（2筆）");

        // 3. 測試讀取收盤快照
        var closeResult = await repo.GetDailyCloseAsync(DateTime.Today);
        Console.WriteLine($" 讀取今天收盤快照：{closeResult.Count} 筆");
        foreach (var s in closeResult)
            Console.WriteLine($"  第{s.Rank}名 {s.Symbol} {s.Name} ${s.Price}");

        // 4. 測試寫入盤中 Session
        var session = new IntradaySessionEntity
        {
            RankType = "Turnover",
            FetchTime = DateTime.Now,
            TradingDate = DateTime.Today,
            IsDataChanged = true
        };
        var sessionId = await repo.SaveIntradaySessionAsync(session);
        Console.WriteLine($"\n 盤中 Session 寫入成功，SessionId = {sessionId}");

        // 5. 測試寫入盤中 Snapshot
        var testSnapshot = new List<IntradaySnapshotEntity>
        {
            new()
            {
                SessionId = sessionId,
                Rank = 1,
                Symbol = "2330",
                Name = "台積電",
                Market = "TW",
                Open = "",
                Price = "903.0",
                Change = "13.0",
                ChangePercent = "1.5%",
                DayHigh = "908.0",
                DayLow = "893.0",
                DayHighLowDiff = "15.0",
                VolK = "28000",
                TurnoverHundredMillion = "72.00"
            }
        };

        await repo.SaveIntradaySnapshotAsync(testSnapshot);
        Console.WriteLine("盤中 Snapshot 寫入成功（1筆）");

        // 6. 測試讀取盤中資料
        var sessions = await repo.GetIntradaySessionsAsync(DateTime.Today);
        Console.WriteLine($" 讀取今天盤中 Session：{sessions.Count} 筆");

        var snapshots = await repo.GetIntradaySnapshotAsync(sessionId);
        Console.WriteLine($" 讀取 Session {sessionId} 的 Snapshot：{snapshots.Count} 筆");
        foreach (var s in snapshots)
            Console.WriteLine($"  第{s.Rank}名 {s.Symbol} {s.Name} ${s.Price}");

        // 7. 測試設定讀寫
        await repo.SetSettingAsync("CustomTime1", "09:35");
        var customTime = await repo.GetSettingAsync("CustomTime1");
        Console.WriteLine($"\n 設定讀寫測試：CustomTime1 = {customTime}");

        // 8. 測試資料庫大小
        var size = await repo.GetDatabaseSizeAsync();
        Console.WriteLine($" 資料庫大小：{size:F2} MB");

        // 9. 測試 Cascade 刪除（刪 Session → Snapshot 自動刪除）
        await repo.CleanIntradayDataAsync();
        var afterDelete = await repo.GetIntradaySessionsAsync(DateTime.Today);
        Console.WriteLine($"\n Cascade 刪除測試：刪除後剩 {afterDelete.Count} 筆 Session");

        // 10. 清除測試資料
        var todayClose = await repo.GetDailyCloseAsync(DateTime.Today);
        if (todayClose.Any())
        {
            db.DailyCloseSnapshots.RemoveRange(todayClose);
            await db.SaveChangesAsync();
        }
        Console.WriteLine(" 測試資料已清除");

        Console.WriteLine("\n===== 全部測試通過 =====");
    }
}
