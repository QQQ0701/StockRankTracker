namespace StockRankTracker.Helpers;

/// <summary>
/// 時間相關的共用工具
/// </summary>
public static class DateTimeHelper
{
    /// <summary>
    /// 現在是否在盤中時間（09:00 ~ 13:29）
    /// </summary>
    public static bool IsMarketHours(DateTime now)
    {
        var time = now.TimeOfDay;
        return time >= new TimeSpan(9, 0, 0) && time < new TimeSpan(13, 30, 0);
    }

    /// <summary>
    /// 現在是否是收盤抓取時間（13:35）
    /// </summary>
    public static bool IsCloseTime(DateTime now)
    {
        return now.Hour == 13 && now.Minute == 35;
    }

    /// <summary>
    /// 取得今天的交易日期（單純取日期部分）
    /// </summary>
    public static DateTime GetTradingDate(DateTime now)
    {
        return now.Date;
    }

    /// <summary>
    /// 今天是否為假日（六日）
    /// </summary>
    public static bool IsWeekend(DateTime now)
    {
        return now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;
    }

    /// <summary>
    /// 今天是否為國定假日
    /// 傳入假日清單進行比對
    /// </summary>
    public static bool IsHoliday(DateTime now, List<DateTime> holidays)
    {
        return holidays.Contains(now.Date);
    }

    /// <summary>
    /// 今天是否為交易日（非六日、非國定假日）
    /// </summary>
    public static bool IsTradingDay(DateTime now, List<DateTime> holidays)
    {
        return !IsWeekend(now) && !IsHoliday(now, holidays);
    }

    /// <summary>
    /// 取得當年度國定假日清單
    /// 每年年初手動更新這份清單
    /// </summary>
    public static List<DateTime> GetHolidays(int year)
    {
        // 2026 年台灣股市休市日（範例，需依實際公布更新）
        if (year == 2026)
        {
            return new List<DateTime>
            {
          // 元旦
        new(2026, 1, 1),
        new(2026, 1, 2),   // 調整放假

        // 春節
        new(2026, 1, 26),  // 除夕前一天（調整）
        new(2026, 1, 27),  // 農曆除夕
        new(2026, 1, 28),  // 春節
        new(2026, 1, 29),  // 春節
        new(2026, 1, 30),  // 春節

        // 2月
        new(2026, 2, 2),   // 春節補假
        new(2026, 2, 27),  // 和平紀念日（調整放假）

        // 和平紀念日
        new(2026, 2, 28),  // 和平紀念日（六）

        // 兒童節 + 清明節
        new(2026, 4, 3),   // 兒童節（調整放假）
        new(2026, 4, 4),   // 清明節（六）
        new(2026, 4, 5),   // 調整放假
        new(2026, 4, 6),   // 調整放假

        // 勞動節
        new(2026, 5, 1),   // 勞動節
        new(2026, 5, 2),   // 調整放假

        // 端午節
        new(2026, 5, 25),  // 端午節

        // 中秋節
        new(2026, 10, 5),  // 中秋節

        // 國慶日
        new(2026, 10, 9),  // 國慶日（調整放假）
        new(2026, 10, 10), // 國慶日（六）
            };
        }

        // 2025 年（如果需要）
        if (year == 2025)
        {
            return new List<DateTime>
            {
                new(2025, 1, 1),   // 元旦
                new(2025, 1, 27),  // 農曆除夕前
                new(2025, 1, 28),  // 農曆除夕
                new(2025, 1, 29),  // 春節
                new(2025, 1, 30),  // 春節
                new(2025, 1, 31),  // 春節
                new(2025, 2, 28),  // 和平紀念日
                new(2025, 4, 3),   // 兒童節
                new(2025, 4, 4),   // 清明節
                new(2025, 5, 30),  // 端午節（調整）
                new(2025, 5, 31),  // 端午節
                new(2025, 10, 6),  // 中秋節
                new(2025, 10, 10), // 國慶日
            };
        }

        // 沒有資料的年份回傳空清單（只靠六日判斷）
        return new List<DateTime>();
    }
    /// <summary>
    /// 從指定日期往前找最近的交易日（跳過週末和國定假日）
    /// 例如傳入週一，會回傳上週五（如果上週五不是假日的話）
    /// </summary>
    public static DateTime GetPreviousTradingDate(DateTime date)
    {
        var holidays = GetHolidays(date.Year);
        var current = date.AddDays(-1);

        // 最多往回找 10 天（避免無限迴圈）
        for (int i = 0; i < 10; i++)
        {
            if (!IsWeekend(current) && !IsHoliday(current, holidays))
            {
                return current.Date;
            }
            current = current.AddDays(-1);

            // 跨年時需要重新取得假日清單
            if (current.Year != date.Year)
            {
                holidays = GetHolidays(current.Year);
            }
        }

        // 理論上不會走到這裡
        return date.AddDays(-1).Date;
    }
}
