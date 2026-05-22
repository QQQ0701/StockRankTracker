namespace StockRankTracker.Contracts.IServices;

/// <summary>
/// 排程服務介面（管理定時抓取和手動觸發）
/// </summary>
public interface ISchedulerService
{
    /// <summary>啟動排程監聽</summary>
    void Start();

    /// <summary>停止排程監聯</summary>
    void Stop();

    /// <summary>
    /// 執行一次抓取
    /// </summary>
    /// <param name="fetchSource">抓取來源："Scheduled" 或 "Manual"</param>
    Task ExecuteFetchAsync(string fetchSource = "Manual");

    /// <summary>是否正在抓取中（供 UI 控制按鈕反白）</summary>
    bool IsFetching { get; }

    /// <summary>抓取狀態變化事件（通知 UI 更新按鈕狀態）</summary>
    event Action<bool>? OnFetchingStateChanged;
}
