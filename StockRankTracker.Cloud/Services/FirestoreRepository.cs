using Google.Cloud.Firestore;
using StockRankTracker.Cloud.Models;

namespace StockRankTracker.Cloud.Services;

/// <summary>
/// Firestore 讀寫
/// 結構：daily_close/{日期}/stocks/rank_001
/// </summary>
public class FirestoreRepository
{
    private readonly FirestoreDb _db;

    public FirestoreRepository()
    {
        // GOOGLE_APPLICATION_CREDENTIALS 環境變數指向 service account JSON
        var projectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
                        ?? throw new Exception("未設定 FIREBASE_PROJECT_ID 環境變數");
        _db = FirestoreDb.Create(projectId);
    }

    /// <summary>
    /// 儲存當日排行（覆寫整份）
    /// </summary>
    public async Task SaveDailyStocksAsync(string dateStr, string timeTag, List<StockEntry> stocks)
    {
        var batch = _db.StartBatch();
        var dayDoc = _db.Collection("daily_close").Document(dateStr);

        batch.Set(dayDoc, new Dictionary<string, object>
    {
        { "date", dateStr },
        { "count", stocks.Count },
        { "updated_at", Timestamp.GetCurrentTimestamp() }
    }, SetOptions.MergeAll);   // ← MergeAll 避免覆蓋其他快照的 metadata

        // stocks 存在 snapshots/{timeTag}/stocks/ 下
        var snapshotDoc = dayDoc.Collection("snapshots").Document(timeTag);
        batch.Set(snapshotDoc, new Dictionary<string, object>
    {
        { "time", timeTag },
        { "count", stocks.Count },
        { "created_at", Timestamp.GetCurrentTimestamp() }
    });

        foreach (var stock in stocks)
        {
            var docId = $"rank_{stock.Rank:D3}";
            var stockDoc = snapshotDoc.Collection("stocks").Document(docId);

            batch.Set(stockDoc, new Dictionary<string, object>
        {
            { "rank", stock.Rank },
            { "symbol", stock.Symbol },
            { "name", stock.Name },
            { "market", stock.Market },
            { "price", stock.Price },
            { "change", stock.Change },
            { "changePercent", stock.ChangePercent },
            { "dayHigh", stock.DayHigh },
            { "dayLow", stock.DayLow },
            { "dayHighLowDiff", stock.DayHighLowDiff },
            { "volK", stock.VolK },
            { "turnoverHundredMillion", stock.TurnoverHundredMillion }
        });
        }

        await batch.CommitAsync();
    }

    /// <summary>
    /// 讀取指定日期的所有股票代號（用於比對新上榜）
    /// </summary>
    public async Task<HashSet<string>> GetSymbolsByDateAsync(string dateStr, string timeTag = "1335")
    {
        var symbols = new HashSet<string>();
        var stocksRef = _db.Collection("daily_close").Document(dateStr)
                      .Collection("snapshots").Document(timeTag)
                      .Collection("stocks"); var snapshot = await stocksRef.GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            if (doc.TryGetValue<int>("rank", out var rank) && rank <= 30
                && doc.TryGetValue<string>("symbol", out var symbol))
            {
                symbols.Add(symbol);
            }
        }

        return symbols;
    }
}