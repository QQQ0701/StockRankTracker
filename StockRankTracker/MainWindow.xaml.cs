using System.Windows;
using System.Windows.Controls;
using StockRankTracker.ViewModels;

namespace StockRankTracker;

public partial class MainWindow : Window
{
    private readonly AfterMarketViewModel _afterMarketVm;
    private readonly SettingsViewModel _settingsVm;

    public MainWindow(
        MainViewModel mainVm,
        IntradayViewModel intradayVm,
        AfterMarketViewModel afterMarketVm,
        SettingsViewModel settingsVm)
    {
        InitializeComponent();
        DataContext = mainVm;
        IntradayTab.DataContext = intradayVm;
        AfterMarketTab.DataContext = afterMarketVm;
        SettingsTab.DataContext = settingsVm;
        _afterMarketVm = afterMarketVm;
        _settingsVm = settingsVm;
    }

    private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is TabControl tabControl)
        {
            switch (tabControl.SelectedIndex)
            {
                case 1: // 盤後頁
                    await _afterMarketVm.LoadAvailableDatesAsync();
                    break;
                case 2: // 設定頁
                    await _settingsVm.LoadSettingsAsync();
                    break;
            }
        }
    }
}
