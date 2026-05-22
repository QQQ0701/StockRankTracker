namespace StockRankTracker.Models.Entities;

/// <summary>
/// 收盤快照表（每天收盤存一份，保留30天）
/// </summary>
public class DailyCloseEntity
{
    public int Id { get; set; }
    public string RankType { get; set; } = "Turnover";  // 排行類型
    public DateTime SnapshotDate { get; set; }           // 交易日期
    public DateTime FetchTime { get; set; }              // 實際抓取時間
    public string DataSource { get; set; } = "Fetch";  // "Fetch" / "Import" / "AutoFill"
    public int Rank { get; set; }                        // 名次 1~100
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Market { get; set; } = "";             // TW / TWO
    public string Open { get; set; } = "";               // 開盤價（預留）
    public string Price { get; set; } = "";
    public string Change { get; set; } = "";
    public string ChangePercent { get; set; } = "";
    public string DayHigh { get; set; } = "";
    public string DayLow { get; set; } = "";
    public string DayHighLowDiff { get; set; } = "";
    public string VolK { get; set; } = "";
    public string TurnoverHundredMillion { get; set; } = "";
}
