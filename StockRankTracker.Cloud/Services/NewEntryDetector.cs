using StockRankTracker.Cloud.Models;

namespace StockRankTracker.Cloud.Services;

/// <summary>
/// 比對新上榜：今天在前 100 名，前一個交易日收盤不在前 100 名
/// </summary>
public class NewEntryDetector
{
    private readonly FirestoreRepository _repo;

    public NewEntryDetector(FirestoreRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// 找出新上榜的股票
    /// </summary>
    public async Task<List<StockEntry>> DetectAsync(
        string todayStr, string previousDateStr, List<StockEntry> todayStocks)
    {
        // 取得前一個交易日的股票代號清單
        var previousSymbols = await _repo.GetSymbolsByDateAsync(previousDateStr);

        // 如果前一天沒資料（第一次跑），就不比對，回傳空
        if (previousSymbols.Count == 0)
        {
            Console.WriteLine($"前一交易日 {previousDateStr} 無資料，跳過新上榜比對。");
            return new List<StockEntry>();
        }

        // 今天有、昨天沒有 → 新上榜
        var newEntries = todayStocks
            .Where(s => !previousSymbols.Contains(s.Symbol))
            .OrderBy(s => s.Rank)
            .ToList();

        return newEntries;
    }
}