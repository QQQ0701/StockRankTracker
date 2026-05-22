using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StockRankTracker.Converters;

/// <summary>
/// 名次變化標籤轉換器（多值轉換器）
/// 需要兩個綁定值：RankChange (int?) 和 IsNewEntry (bool)
/// 
/// 新上榜     → "NEW"  藍底白字
/// 正數(上升) → "↑N"   紅底白字
/// 負數(下降) → "↓N"   綠底白字
/// 零(不變)   → "-"    灰底白字
/// null(無資料) → 不顯示
/// </summary>
public class RankBadgeTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return "";

        var isNewEntry = values[1] is bool b && b;

        // 新上榜
        if (isNewEntry)
            return "NEW";

        // 有名次變化
        if (values[0] is int rankChange)
        {
            if (rankChange > 0) return $"↑{rankChange}";
            if (rankChange < 0) return $"↓{Math.Abs(rankChange)}";
            return "-";
        }

        // null → 無資料（第一天使用）
        return "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 名次變化背景色轉換器（多值轉換器）
/// </summary>
public class RankBadgeBackgroundConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(41, 121, 255));   // 新上榜藍
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(255, 80, 80));      // 上升紅
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(77, 208, 120));   // 下降綠
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(85, 85, 85));      // 不變灰
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);       // 無資料

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return TransparentBrush;

        var isNewEntry = values[1] is bool b && b;

        if (isNewEntry)
            return BlueBrush;

        if (values[0] is int rankChange)
        {
            if (rankChange > 0) return RedBrush;
            if (rankChange < 0) return GreenBrush;
            return GrayBrush;
        }

        return TransparentBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
