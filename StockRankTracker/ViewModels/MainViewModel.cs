using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StockRankTracker.ViewModels;

/// <summary>
/// 主視窗 ViewModel（管理 Tab 切換狀態）
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
