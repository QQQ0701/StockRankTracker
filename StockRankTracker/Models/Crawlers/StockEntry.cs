namespace StockRankTracker.Models.Crawlers;

/// <summary>
/// 爬蟲抓取的單筆股票原始資料
/// </summary>
public class StockEntry
{
    public int Rank { get; set; }
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Market { get; set; } = "";        // TW / TWO
    public string Open { get; set; } = "";           // 開盤價（預留，目前暫用最低價替代）
    public string Price { get; set; } = "";           // 現價（收盤價）
    public string Change { get; set; } = "";          // 漲跌金額
    public string ChangePercent { get; set; } = "";   // 漲跌幅
    public string DayHigh { get; set; } = "";         // 最高
    public string DayLow { get; set; } = "";          // 最低
    public string DayHighLowDiff { get; set; } = "";  // 價差
    public string VolK { get; set; } = "";            // 量(張)
    public string TurnoverHundredMillion { get; set; } = ""; // 成交額(億)
}
