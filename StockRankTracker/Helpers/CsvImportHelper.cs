using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using StockRankTracker.Models.Entities;

namespace StockRankTracker.Helpers;

/// <summary>
/// CSV 匯入解析工具（XQ 匯出格式）
/// </summary>
public static class CsvImportHelper
{
    /// <summary>
    /// 解析結果
    /// </summary>
    public class ImportResult
    {
        public bool Success { get; set; }
        public DateTime TradingDate { get; set; }
        public List<DailyCloseEntity> Entities { get; set; } = new();
        public string ErrorMessage { get; set; } = "";
        public string FileName { get; set; } = "";
    }

    /// <summary>
    /// 解析單一 CSV 檔案
    /// </summary>
    public static ImportResult ParseCsv(string csvPath)
    {
        var result = new ImportResult
        {
            FileName = Path.GetFileName(csvPath)
        };

        try
        {
            if (!File.Exists(csvPath))
            {
                result.ErrorMessage = "檔案不存在";
                return result;
            }

            // 讀取檔案（Big5 編碼）
            string content;
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                content = File.ReadAllText(csvPath, Encoding.GetEncoding("big5"));
            }
            catch
            {
                content = File.ReadAllText(csvPath, Encoding.UTF8);
            }

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // 解析日期（第2行：資料日期：2026年  4月 23日）
            DateTime tradingDate = DateTime.MinValue;
            foreach (var line in lines)
            {
                if (line.Contains("資料日期"))
                {
                    var cleaned = line.Replace("資料日期：", "")
                        .Replace("年", "/").Replace("月", "/").Replace("日", "").Trim();
                    cleaned = Regex.Replace(cleaned, @"\s+", "");
                    if (DateTime.TryParse(cleaned, out var parsed))
                        tradingDate = parsed.Date;
                    break;
                }
            }

            if (tradingDate == DateTime.MinValue)
            {
                result.ErrorMessage = "無法解析日期";
                return result;
            }

            result.TradingDate = tradingDate;
            var fetchTime = tradingDate.AddHours(13).AddMinutes(35);

            // 解析資料行
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim().TrimEnd('\r');
                if (string.IsNullOrEmpty(line)) continue;
                if (!char.IsDigit(line[0])) continue;

                try
                {
                    var cleaned = line.Replace("\"", "");
                    var parts = cleaned.Split(new[] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 13) continue;

                    int rank = int.Parse(parts[0].Trim());
                    string rawSymbol = parts[1].Trim();
                    string name = parts[2].Trim();
                    string volK = parts[5].Trim();

                    // 根據欄位數量判斷格式
                    string dayHigh, dayLow, price, open, changePercent, turnover, changeValue;

                    if (parts.Length >= 15)
                    {
                        // 格式A（有「未過價(日期)」和「區間漲跌%」）：15欄
                        dayHigh = parts[8].Trim();
                        dayLow = parts[9].Trim();
                        price = parts[10].Trim();
                        open = parts[11].Trim();
                        changePercent = parts[12].Trim();
                        turnover = parts[13].Trim();
                        changeValue = parts[14].Trim();
                    }
                    else
                    {
                        // 格式B（最新一天，少兩欄）：13欄
                        dayHigh = parts[6].Trim();
                        dayLow = parts[7].Trim();
                        price = parts[8].Trim();
                        open = parts[9].Trim();
                        changePercent = parts[10].Trim();
                        turnover = parts[11].Trim();
                        changeValue = parts[12].Trim();
                    }

                    // 以下不變...
                    string symbol = rawSymbol;
                    string market = "";
                    if (rawSymbol.Contains('.'))
                    {
                        var symbolParts = rawSymbol.Split('.');
                        symbol = symbolParts[0];
                        market = symbolParts[1];
                    }

                    string change = changeValue;
                    if (double.TryParse(changeValue, out double cv))
                        change = cv >= 0 ? $"+{cv}" : cv.ToString();

                    string changePctDisplay = changePercent;
                    if (double.TryParse(changePercent, out double pv))
                        changePctDisplay = pv >= 0 ? $"+{pv}%" : $"{pv}%";

                    string dayHighLowDiff = "";
                    if (double.TryParse(dayHigh, out double h) && double.TryParse(dayLow, out double l))
                        dayHighLowDiff = (h - l).ToString();

                    result.Entities.Add(new DailyCloseEntity
                    {
                        RankType = "Turnover",
                        SnapshotDate = tradingDate,
                        FetchTime = fetchTime,
                        Rank = rank,
                        Symbol = symbol,
                        Name = name,
                        Market = market,
                        Open = open,
                        Price = price,
                        Change = change,
                        ChangePercent = changePctDisplay,
                        DayHigh = dayHigh,
                        DayLow = dayLow,
                        DayHighLowDiff = dayHighLowDiff,
                        VolK = volK,
                        TurnoverHundredMillion = turnover
                    });
                }
                catch
                {
                    // 單行解析失敗，跳過繼續
                }
            }

            result.Success = result.Entities.Count > 0;
            if (!result.Success)
                result.ErrorMessage = "沒有解析到任何資料";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}
