using System.Windows.Controls;
using StockRankTracker.ViewModels;

namespace StockRankTracker.Views;

public partial class IntradayView : UserControl
{
    public IntradayView()
    {
        InitializeComponent();
    }

    private void OnFilterAll_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is IntradayViewModel vm)
        {
            vm.IsFilterNewEntry = false;
        }
    }

    private void OnFilterNew_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is IntradayViewModel vm)
        {
            vm.IsFilterNewEntry = true;
        }
    }
}
