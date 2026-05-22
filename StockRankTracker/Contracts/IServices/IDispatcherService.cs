namespace StockRankTracker.Contracts.IServices;

/// <summary>
/// UI 執行緒調度服務介面
/// 讓 ViewModel 不直接耦合 WPF 的 Dispatcher
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// 在 UI 執行緒上執行動作
    /// </summary>
    void RunOnUI(Action action);
}
