using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StockRankTracker.Converters;

/// <summary>
/// 字串轉 Visibility 轉換器
/// 有內容 → Visible
/// 空白或 null → Collapsed
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text && !string.IsNullOrWhiteSpace(text))
            return Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
