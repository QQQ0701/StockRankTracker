using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StockRankTracker.Converters;

/// <summary>
/// 市場標籤文字轉換器
/// TW  → "上市"
/// TWO → "上櫃"
/// </summary>
public class MarketTagTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string market)
        {
            return market switch
            {
                "TW" => "上市",
                "TWO" => "上櫃",
                _ => market
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 市場標籤背景色轉換器
/// TW  → 深藍色
/// TWO → 紫色
/// </summary>
public class MarketTagBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush ListedBrush = new(Color.FromRgb(30, 60, 114));   // 上市深藍
    private static readonly SolidColorBrush OtcBrush = new(Color.FromRgb(102, 51, 153));     // 上櫃紫色
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(85, 85, 85));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string market)
        {
            return market switch
            {
                "TW" => ListedBrush,
                "TWO" => OtcBrush,
                _ => DefaultBrush
            };
        }
        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
