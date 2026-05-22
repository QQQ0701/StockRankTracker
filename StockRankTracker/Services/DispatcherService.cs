using StockRankTracker.Contracts.IServices;

namespace StockRankTracker.Services;

/// <summary>
/// WPF Dispatcher 的封裝實作
/// </summary>
public class DispatcherService : IDispatcherService
{
    /// <summary>
    /// 在 UI 執行緒上執行動作（自動判斷是否需要切換執行緒）
    /// </summary>
    public void RunOnUI(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        if (app.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            app.Dispatcher.Invoke(action);
        }
    }
}