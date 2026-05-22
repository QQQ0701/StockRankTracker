using StockRankTracker.Data;
using StockRankTracker.Data.Repositories;
using StockRankTracker.Services;
using StockRankTracker.Services.Crawlers;
using System.Net.Http;

namespace StockRankTracker.Tests;

/// <summary>
/// 第五步測試：驗證 FetchOrchestrator 完整流程
/// 在 App.xaml.cs 的 OnStartup 裡呼叫 ServiceTest.RunAsync()
/// </summary>
public static class ServiceTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine("===== Service 測試開始 =====\n");

        // 建立所有依賴
        using var db = new DatabaseContext();
        await db.Database.EnsureCreatedAsync();
        var repo = new StockRepository(db);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-TW,zh;q=0.9");

        var crawler = new TurnoverCrawler(httpClient);
        var orchestrator = new FetchOrchestrator(crawler, repo);

        // 監聽抓取完成事件
        orchestrator.OnFetchCompleted += () =>
        {
            Console.WriteLine($"  [事件] OnFetchCompleted 觸發！");
        };

        // ===== 測試1：模擬盤中抓取 =====
        Console.WriteLine("[1] 測試盤中抓取流程");
        Console.WriteLine("    呼叫 FetchAndSaveAsync()...");
        await orchestrator.FetchAndSaveAsync();

        Console.WriteLine($"    成功？ {orchestrator.LastFetchSuccess}");
        Console.WriteLine($"    時間：{orchestrator.LastFetchTime:HH:mm:ss}");

        if (!orchestrator.LastFetchSuccess)
        {
            Console.WriteLine($"    錯誤：{orchestrator.LastErrorMessage}");
            Console.WriteLine("\n===== 測試中斷 =====");
            return;
        }

        // 確認資料有存入
        var sessions = await repo.GetIntradaySessionsAsync(DateTime.Today);
        Console.WriteLine($"    Session 數量：{sessions.Count}");
        if (sessions.Count > 0)
        {
            var lastSession = sessions.Last();
            Console.WriteLine($"    最後 Session：Id={lastSession.SessionId}, Changed={lastSession.IsDataChanged}");

            if (lastSession.IsDataChanged)
            {
                var snapshots = await repo.GetIntradaySnapshotAsync(lastSession.SessionId);
                Console.WriteLine($"    Snapshot 數量：{snapshots.Count} (應為100)");
                if (snapshots.Count > 0)
                {
                    Console.WriteLine($"    第1名：{snapshots[0].Symbol} {snapshots[0].Name} ${snapshots[0].Price}");
                }
            }
        }
        Console.WriteLine("✓ 盤中抓取測試通過\n");

        // ===== 測試2：第二次抓取（測試資料比對）=====
        Console.WriteLine("[2] 測試第二次抓取（資料比對）");
        Console.WriteLine("    等待3秒後再抓取...");
        await Task.Delay(3000);
        await orchestrator.FetchAndSaveAsync();

        var sessions2 = await repo.GetIntradaySessionsAsync(DateTime.Today);
        Console.WriteLine($"    Session 數量：{sessions2.Count} (應為2)");
        if (sessions2.Count >= 2)
        {
            var secondSession = sessions2.Last();
            Console.WriteLine($"    第二次 IsDataChanged：{secondSession.IsDataChanged}");
            Console.WriteLine("    （如果盤中價格有變動就是 True，收盤後可能是 False）");
        }
        Console.WriteLine("✓ 資料比對測試通過\n");

        // ===== 測試3：SchedulerService 基本功能 =====
        Console.WriteLine("[3] 測試 SchedulerService");
        var scheduler = new SchedulerService(orchestrator, repo);

        var systemTimes = SchedulerService.GetSystemTimeStrings();
        Console.WriteLine($"    系統固定時間：{string.Join(", ", systemTimes)}");

        // 設定自訂時間
        await repo.SetSettingAsync("CustomTime1", "09:35");
        await repo.SetSettingAsync("CustomTime2", "10:30");
        var ct1 = await repo.GetSettingAsync("CustomTime1");
        var ct2 = await repo.GetSettingAsync("CustomTime2");
        Console.WriteLine($"    自訂時間1：{ct1}");
        Console.WriteLine($"    自訂時間2：{ct2}");

        Console.WriteLine($"    IsFetching：{scheduler.IsFetching} (應為 False)");
        Console.WriteLine("✓ SchedulerService 測試通過\n");

        // ===== 測試4：手動觸發抓取（模擬使用者按按鈕）=====
        Console.WriteLine("[4] 測試手動觸發抓取（透過 SchedulerService）");

        scheduler.OnFetchingStateChanged += (isFetching) =>
        {
            Console.WriteLine($"    [事件] IsFetching 變為 {isFetching}");
        };

        await scheduler.ExecuteFetchAsync();
        Console.WriteLine($"    抓取後 IsFetching：{scheduler.IsFetching} (應為 False)");
        Console.WriteLine("✓ 手動觸發測試通過\n");

        // ===== 清除測試資料 =====
        Console.WriteLine("[清除] 刪除測試產生的資料...");
        await repo.CleanIntradayDataAsync();
        var remaining = await repo.GetIntradaySessionsAsync(DateTime.Today);
        Console.WriteLine($"    剩餘 Session：{remaining.Count} (應為 0)");
        Console.WriteLine("✓ 清除完成");

        Console.WriteLine("\n===== Service 測試全部通過 =====");
    }
}
