namespace StockRankTracker.Models.Entities;

/// <summary>
/// 盤中批次記錄表（記錄每次抓取行為，隔天自動清除）
/// </summary>
public class IntradaySessionEntity
{
    public int SessionId { get; set; }
    public string RankType { get; set; } = "Turnover";  // 排行類型
    public DateTime FetchTime { get; set; }              // 抓取時間
    public DateTime TradingDate { get; set; }            // 交易日
    public bool IsDataChanged { get; set; }              // 與上一筆是否有差異
    public string FetchSource { get; set; } = "Scheduled";  // "Scheduled" 或 "Manual"
}
