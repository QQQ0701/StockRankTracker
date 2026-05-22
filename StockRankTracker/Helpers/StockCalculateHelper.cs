using StockRankTracker.Models.Crawlers;
using StockRankTracker.Models.Display;
using StockRankTracker.Models.Entities;

namespace StockRankTracker.Helpers;

/// <summary>
/// 股票相關的計算工具
/// </summary>
public static class StockCalculateHelper
{
    /// <summary>
    /// 計算名次變化
    /// 拿今天的排名跟昨天收盤排名比對
    /// </summary>
    /// <param name="currentRank">今天的名次</param>
    /// <param name="symbol">股票代號</param>
    /// <param name="yesterdayClose">昨天收盤的前100名資料</param>
    /// <returns>
    /// 正數 = 名次上升（例如昨天第5，今天第2，回傳 3）
    /// 負數 = 名次下降（例如昨天第2，今天第5，回傳 -3）
    /// null = 昨天不在前100名（新上榜）
    /// </returns>
    public static int? CalcRankChange(int currentRank, string symbol, List<DailyCloseEntity> yesterdayClose)
    {
        // 沒有昨天的資料，無法比對
        if (yesterdayClose == null || yesterdayClose.Count == 0)
            return null;

        var yesterday = yesterdayClose.FirstOrDefault(s => s.Symbol == symbol);

        // 昨天不在前100名 → 新上榜
        if (yesterday == null)
            return null;

        // 名次變化 = 昨天名次 - 今天名次（正數代表上升）
        return yesterday.Rank - currentRank;
    }

    /// <summary>
    /// 判斷是否為新上榜
    /// 今天在前30名，但昨天不在前30名
    /// </summary>
    public static bool IsNewEntry(int currentRank, string symbol, List<DailyCloseEntity> yesterdayClose)
    {
        if (currentRank > 30)
            return false;

        if (yesterdayClose == null || yesterdayClose.Count == 0)
            return false;

        // 昨天前30名的股票代號
        var yesterdayTop30Symbols = yesterdayClose
            .Where(s => s.Rank <= 30)
            .Select(s => s.Symbol)
            .ToHashSet();

        // 今天在前30，昨天不在前30 → 新上榜
        return !yesterdayTop30Symbols.Contains(symbol);
    }

    /// <summary>
    /// 計算統計資料（從100名資料中計算）
    /// </summary>
    public static StatisticsModel CalcStatistics(List<StockEntry> stocks)
    {
        var stats = new StatisticsModel();

        foreach (var s in stocks)
        {
            // 上市 / 上櫃
            if (s.Market == "TW")
                stats.ListedCount++;
            else if (s.Market == "TWO")
                stats.OtcCount++;

            // 上漲 / 平盤 / 下跌（從 Change 判斷）
            if (TryParseChange(s.Change, out double change))
            {
                if (change > 0)
                    stats.UpCount++;
                else if (change < 0)
                    stats.DownCount++;
                else
                    stats.FlatCount++;
            }
            else
            {
                // 無法解析，當作平盤
                stats.FlatCount++;
            }
        }

        return stats;
    }

    /// <summary>
    /// 將 StockEntry（爬蟲資料）轉換為 StockDisplayModel（UI 顯示用）
    /// </summary>
    public static List<StockDisplayModel> ToDisplayModels(
        List<StockEntry> stocks,
        List<DailyCloseEntity> yesterdayClose,
        int displayLimit = 30)
    {
        var result = new List<StockDisplayModel>();

        foreach (var s in stocks.Where(s => s.Rank <= displayLimit))
        {
            var rankChange = CalcRankChange(s.Rank, s.Symbol, yesterdayClose);
            var isNew = IsNewEntry(s.Rank, s.Symbol, yesterdayClose);

            result.Add(new StockDisplayModel
            {
                Rank = s.Rank,
                Symbol = s.Symbol,
                Name = s.Name,
                Market = s.Market,
                Open = s.Open,
                Price = s.Price,
                DayHigh = s.DayHigh,
                DayLow = s.DayLow,
                Change = s.Change,
                ChangePercent = s.ChangePercent,
                VolK = s.VolK,
                TurnoverHundredMillion = s.TurnoverHundredMillion,
                RankChange = isNew ? null : rankChange,
                IsNewEntry = isNew
            });
        }

        return result;
    }

    /// <summary>
    /// 嘗試解析漲跌金額字串為數字
    /// 處理各種格式："+3.6", "-2.1", "0", "0.0", "+0.0"
    /// </summary>
    private static bool TryParseChange(string changeStr, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(changeStr))
            return false;

        // 移除正號（double.TryParse 不認 "+"）
        var cleaned = changeStr.Replace("+", "").Trim();
        return double.TryParse(cleaned, out value);
    }
}
