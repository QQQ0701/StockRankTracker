namespace StockRankTracker.Models.Display;

/// <summary>
/// 統計列資料（從100名資料中計算）
/// </summary>
public class StatisticsModel
{
    public int ListedCount { get; set; }      // 上市數量（TW）
    public int OtcCount { get; set; }         // 上櫃數量（TWO）
    public int UpCount { get; set; }          // 上漲數量
    public int FlatCount { get; set; }        // 平盤數量
    public int DownCount { get; set; }        // 下跌數量
}
