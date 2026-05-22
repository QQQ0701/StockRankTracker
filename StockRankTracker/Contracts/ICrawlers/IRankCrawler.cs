using StockRankTracker.Models.Crawlers;

namespace StockRankTracker.Contracts.ICrawlers;

/// <summary>
/// 排行爬蟲介面
/// </summary>
public interface IRankCrawler
{
    /// <summary>
    /// 抓取排行資料
    /// </summary>
    /// <param name="limit">抓取前幾名（預設100）</param>
    /// <returns>股票清單</returns>
    Task<List<StockEntry>> FetchAsync(int limit = 100);
}
