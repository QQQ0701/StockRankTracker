using System.Windows.Controls;
using StockRankTracker.ViewModels;

namespace StockRankTracker.Views;

public partial class AfterMarketView : UserControl
{
    public AfterMarketView()
    {
        InitializeComponent();
    }

    private void OnFilterAll_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AfterMarketViewModel vm)
        {
            vm.IsFilterNewEntry = false;
        }
    }

    private void OnFilterNew_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AfterMarketViewModel vm)
        {
            vm.IsFilterNewEntry = true;
        }
    }
}
