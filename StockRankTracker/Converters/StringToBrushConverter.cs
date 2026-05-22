using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StockRankTracker.Converters;

/// <summary>
/// 將顏色字串（如 "#FF5050"）轉換為 SolidColorBrush
/// </summary>
public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorStr && !string.IsNullOrWhiteSpace(colorStr))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                return new SolidColorBrush(color);
            }
            catch
            {
                // 解析失敗回傳灰色
            }
        }
        return new SolidColorBrush(Color.FromRgb(136, 136, 136));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
