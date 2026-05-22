namespace StockRankTracker.Models.Display;

/// <summary>
/// UI 顯示用的股票模型（綁定到盤中/盤後的 DataGrid）
/// </summary>
public class StockDisplayModel
{
    // 基本資訊
    public int Rank { get; set; }
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Market { get; set; } = "";              // TW / TWO

    // K棒資料（開高低收）
    public string Open { get; set; } = "";                // 開盤價（目前暫用最低價）
    public string Price { get; set; } = "";               // 現價（收盤價）
    public string DayHigh { get; set; } = "";
    public string DayLow { get; set; } = "";

    // 漲跌
    public string Change { get; set; } = "";              // 漲跌金額
    public string ChangePercent { get; set; } = "";       // 漲跌幅

    // 成交
    public string VolK { get; set; } = "";                // 量(張)
    public string TurnoverHundredMillion { get; set; } = ""; // 成交額(億)

    // 名次變化（與前一天收盤比對）
    public int? RankChange { get; set; }                  // 正數=上升, 負數=下降, null=新上榜
    public bool IsNewEntry { get; set; }                  // 是否新上榜
}
