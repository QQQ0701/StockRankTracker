using System.Windows.Input;

namespace StockRankTracker.Helpers;

/// <summary>
/// 簡易的 RelayCommand 實作（支援非同步執行）
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) => await _execute();

    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// 通知 UI 重新檢查按鈕是否可用
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        if (app.Dispatcher.CheckAccess())
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            app.Dispatcher.Invoke(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }
    }
}