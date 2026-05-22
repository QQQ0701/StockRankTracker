using StockRankTracker.Services.Crawlers;
using System.Net.Http;

namespace StockRankTracker.Tests;

/// <summary>
/// 第三步測試：驗證爬蟲是否正常運作
/// 在 App.xaml.cs 的 OnStartup 裡呼叫 CrawlerTest.RunAsync()
/// 測試完成後記得移除
/// </summary>
public static class CrawlerTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine("===== 爬蟲測試開始 =====\n");

        // 建立 HttpClient（加上 Header 模擬瀏覽器）
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept-Language", "zh-TW,zh;q=0.9");

        var crawler = new TurnoverCrawler(client);

        // 1. 抓取前100名
        Console.WriteLine("[1] 開始抓取成交金額排行前100名...");
        var stocks = await crawler.FetchAsync(100);
        Console.WriteLine($"✓ 抓取完成，共 {stocks.Count} 筆\n");

        // 2. 顯示前10名，確認資料格式
        Console.WriteLine("[2] 前10名資料：");
        Console.WriteLine($"{"名次",4} {"代號",-8} {"股名",-8} {"市場",-5} {"開盤",8} {"股價",8} {"漲跌",8} {"漲跌幅",9} {"最高",8} {"最低",8} {"金額(億)",8}");
        Console.WriteLine(new string('-', 100));

        foreach (var s in stocks.Take(10))
        {
            Console.WriteLine(
                $"{s.Rank,4} {s.Symbol,-8} {s.Name,-8} {s.Market,-5} {s.Open,8} {s.Price,8} {s.Change,8} {s.ChangePercent,9} " +
                $"{s.DayHigh,8} {s.DayLow,8} {s.TurnoverHundredMillion,8}");
        }

        // 3. 驗證 Market 欄位拆分
        Console.WriteLine($"\n[3] Market 欄位驗證：");
        var twCount = stocks.Count(s => s.Market == "TW");
        var twoCount = stocks.Count(s => s.Market == "TWO");
        var otherCount = stocks.Count(s => s.Market != "TW" && s.Market != "TWO");
        Console.WriteLine($"✓ 上市(TW)：{twCount} 檔");
        Console.WriteLine($"✓ 上櫃(TWO)：{twoCount} 檔");
        if (otherCount > 0)
            Console.WriteLine($"✗ 未知市場：{otherCount} 檔（需檢查）");
        else
            Console.WriteLine("✓ 沒有未知市場，Market 拆分正確");

        // 4. 驗證 Open 欄位（目前應該等於 DayLow）
        Console.WriteLine($"\n[4] Open 欄位驗證（應等於 DayLow）：");
        var openMatchCount = stocks.Count(s => s.Open == s.DayLow);
        Console.WriteLine($"✓ Open == DayLow 的數量：{openMatchCount} / {stocks.Count}");

        // 5. 驗證沒有重複的股票代號
        Console.WriteLine($"\n[5] 重複檢查：");
        var distinctCount = stocks.Select(s => s.Symbol).Distinct().Count();
        if (distinctCount == stocks.Count)
            Console.WriteLine($"✓ 無重複，共 {distinctCount} 檔不同股票");
        else
            Console.WriteLine($"✗ 有重複！總筆數 {stocks.Count}，不重複 {distinctCount}");

        // 6. 驗證排名是否連續
        Console.WriteLine($"\n[6] 排名連續性檢查：");
        var ranks = stocks.Select(s => s.Rank).ToList();
        bool isContinuous = true;
        for (int i = 0; i < ranks.Count - 1; i++)
        {
            if (ranks[i + 1] != ranks[i] + 1)
            {
                Console.WriteLine($"✗ 排名不連續：第{ranks[i]}名後面是第{ranks[i + 1]}名");
                isContinuous = false;
                break;
            }
        }
        if (isContinuous)
            Console.WriteLine($"✓ 排名連續，從第{ranks.First()}名到第{ranks.Last()}名");

        Console.WriteLine("\n===== 爬蟲測試完成 =====");
    }
}
