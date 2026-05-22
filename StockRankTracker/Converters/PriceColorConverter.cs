using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StockRankTracker.Converters;

/// <summary>
/// 股價顏色轉換器
/// 正數 → 紅色（上漲）
/// 負數 → 綠色（下跌）
/// 零   → 灰色（平盤）
/// </summary>
public class PriceColorConverter : IValueConverter
{
    // 預設顏色
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(255, 80, 80));    // 上漲紅
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(77, 208, 120)); // 下跌綠
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(153, 153, 153)); // 平盤灰

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
            return GrayBrush;

        // 移除 + 號和 % 號，嘗試解析
        var cleaned = text.Replace("+", "").Replace("%", "").Trim();

        if (double.TryParse(cleaned, out double number))
        {
            if (number > 0) return RedBrush;
            if (number < 0) return GreenBrush;
        }

        return GrayBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
