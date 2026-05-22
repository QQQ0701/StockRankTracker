namespace StockRankTracker.Models.Entities;

/// <summary>
/// 盤中快照資料表（每次盤中抓取的100名股票，透過 Cascade 隨 Session 清除）
/// </summary>
public class IntradaySnapshotEntity
{
    public int SnapshotId { get; set; }
    public int SessionId { get; set; }               // FK → IntradaySession
    public int Rank { get; set; }                     // 名次 1~100
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Market { get; set; } = "";          // TW / TWO
    public string Open { get; set; } = "";            // 開盤價（預留）
    public string Price { get; set; } = "";
    public string Change { get; set; } = "";
    public string ChangePercent { get; set; } = "";
    public string DayHigh { get; set; } = "";
    public string DayLow { get; set; } = "";
    public string DayHighLowDiff { get; set; } = "";
    public string VolK { get; set; } = "";
    public string TurnoverHundredMillion { get; set; } = "";
}
