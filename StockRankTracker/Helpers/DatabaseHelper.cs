using StockRankTracker.Models.Crawlers;
using StockRankTracker.Models.Entities;
using System.IO;

namespace StockRankTracker.Helpers;

/// <summary>
/// 資料庫相關的共用工具
/// </summary>
public static class DatabaseHelper
{
    /// <summary>
    /// 取得資料庫檔案大小（MB）
    /// </summary>
    public static double GetDatabaseSize(string dbPath)
    {
        if (!File.Exists(dbPath))
            return 0.0;

        var fileInfo = new FileInfo(dbPath);
        return fileInfo.Length / (1024.0 * 1024.0);
    }

    /// <summary>
    /// 比對兩筆資料是否有變化
    /// 用來判斷假日或休市（資料完全沒變就不存）
    /// 比對方式：取前30名的股票代號 + 名次，看是否完全一致
    /// </summary>
    public static bool IsDataChanged(List<StockEntry> current, List<IntradaySnapshotEntity>? previous)
    {
        // 沒有前一筆資料 → 當作有變化（第一次抓取）
        if (previous == null || previous.Count == 0)
            return true;

        // 取前30名來比對（不用比100名，前30名變化就足以判斷）
        var currentTop30 = current
            .Where(s => s.Rank <= 30)
            .OrderBy(s => s.Rank)
            .Select(s => $"{s.Rank}:{s.Symbol}:{s.Price}")
            .ToList();

        var previousTop30 = previous
            .Where(s => s.Rank <= 30)
            .OrderBy(s => s.Rank)
            .Select(s => $"{s.Rank}:{s.Symbol}:{s.Price}")
            .ToList();

        // 數量不同 → 有變化
        if (currentTop30.Count != previousTop30.Count)
            return true;

        // 逐筆比對
        for (int i = 0; i < currentTop30.Count; i++)
        {
            if (currentTop30[i] != previousTop30[i])
                return true;
        }

        return false;
    }

    /// <summary>
    /// 將 StockEntry（爬蟲資料）轉換為 DailyCloseEntity（收盤快照）
    /// </summary>
    public static List<DailyCloseEntity> ToDailyCloseEntities(
        List<StockEntry> stocks, DateTime tradingDate, DateTime fetchTime)
    {
        return stocks.Select(s => new DailyCloseEntity
        {
            RankType = "Turnover",
            SnapshotDate = tradingDate,
            FetchTime = fetchTime,
            Rank = s.Rank,
            Symbol = s.Symbol,
            Name = s.Name,
            Market = s.Market,
            Open = s.Open,
            Price = s.Price,
            Change = s.Change,
            ChangePercent = s.ChangePercent,
            DayHigh = s.DayHigh,
            DayLow = s.DayLow,
            DayHighLowDiff = s.DayHighLowDiff,
            VolK = s.VolK,
            TurnoverHundredMillion = s.TurnoverHundredMillion
        }).ToList();
    }

    /// <summary>
    /// 將 StockEntry（爬蟲資料）轉換為 IntradaySnapshotEntity（盤中快照）
    /// </summary>
    public static List<IntradaySnapshotEntity> ToIntradaySnapshotEntities(
        List<StockEntry> stocks, int sessionId)
    {
        return stocks.Select(s => new IntradaySnapshotEntity
        {
            SessionId = sessionId,
            Rank = s.Rank,
            Symbol = s.Symbol,
            Name = s.Name,
            Market = s.Market,
            Open = s.Open,
            Price = s.Price,
            Change = s.Change,
            ChangePercent = s.ChangePercent,
            DayHigh = s.DayHigh,
            DayLow = s.DayLow,
            DayHighLowDiff = s.DayHighLowDiff,
            VolK = s.VolK,
            TurnoverHundredMillion = s.TurnoverHundredMillion
        }).ToList();
    }


}
