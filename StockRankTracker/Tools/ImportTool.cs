using System.IO;
using System.Text;
using StockRankTracker.Data;
using StockRankTracker.Data.Repositories;
using StockRankTracker.Models.Entities;

namespace StockRankTracker.Tools;

/// <summary>
/// 匯入 CSV 收盤資料工具
/// 用完即可刪除此檔案
/// 
/// 使用方式：在 App.xaml.cs 的 OnStartup 裡加入：
///   await ImportTool.ImportCsvAsync("CSV檔案的完整路徑");
/// 匯入完成後移除該行
/// </summary>
public static class ImportTool
{
    public static async Task ImportCsvAsync(string csvPath)
    {
        Console.WriteLine("===== 開始匯入 CSV =====");

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"✗ 檔案不存在：{csvPath}");
            return;
        }

        // 讀取檔案（嘗試不同編碼）
        string content;
        try
        {
            // 先嘗試 Big5/CP950 編碼
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            content = File.ReadAllText(csvPath, Encoding.GetEncoding("big5"));
        }
        catch
        {
            content = File.ReadAllText(csvPath, Encoding.UTF8);
        }

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"  讀取到 {lines.Length} 行");

        // 解析日期（第2行：資料日期：2026年  4月 23日）
        DateTime tradingDate = DateTime.Today;
        foreach (var line in lines)
        {
            if (line.Contains("資料日期"))
            {
                var cleaned = line.Replace("資料日期：", "").Replace("年", "/").Replace("月", "/").Replace("日", "").Trim();
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", "");
                if (DateTime.TryParse(cleaned, out var parsed))
                {
                    tradingDate = parsed.Date;
                }
                break;
            }
        }
        Console.WriteLine($"  交易日期：{tradingDate:yyyy/MM/dd}");

        // 找到資料開始行（序號開頭）
        var entities = new List<DailyCloseEntity>();
        var fetchTime = tradingDate.AddHours(13).AddMinutes(35); // 模擬 13:35 收盤時間

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (string.IsNullOrEmpty(line)) continue;

            // 資料行以數字開頭（序號）
            var firstChar = line[0];
            if (!char.IsDigit(firstChar)) continue;

            try
            {
                // 清理格式：移除引號，用 tab 和逗號分割
                var cleaned = line.Replace("\"", "");
                // 分割方式：tab 分隔序號，逗號分隔其他欄位
                var parts = cleaned.Split(new[] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 14) continue;

                // 解析欄位
                // 序號, 代碼, 商品, 成交, 漲幅%, 總量, 收盤價(前日), 區間漲幅%, 最高價, 最低價, 收盤價, 開盤價, 成交金額(億), 排行名次
                int rank = int.Parse(parts[0].Trim());
                string rawSymbol = parts[1].Trim();
                string name = parts[2].Trim();
                string price = parts[3].Trim();           // 成交（當日收盤）
                string changePercent = parts[4].Trim();    // 漲幅%
                string volK = parts[5].Trim();             // 總量(張)
                string prevClose = parts[6].Trim();        // 前日收盤
                // parts[7] = 區間漲幅%（跳過）
                string dayHigh = parts[8].Trim();
                string dayLow = parts[9].Trim();
                string closePrice = parts[10].Trim();      // 收盤價
                string openPrice = parts[11].Trim();       // 開盤價
                string turnover = parts[12].Trim();        // 成交金額(億)
                int rankFromCsv = int.Parse(parts[13].Trim());

                // 拆分 Market
                string symbol = rawSymbol;
                string market = "";
                if (rawSymbol.Contains('.'))
                {
                    var symbolParts = rawSymbol.Split('.');
                    symbol = symbolParts[0];
                    market = symbolParts[1];
                }

                // 計算漲跌金額
                string change = "";
                if (double.TryParse(price, out double priceVal) && double.TryParse(prevClose, out double prevVal))
                {
                    var changeVal = priceVal - prevVal;
                    change = changeVal >= 0 ? $"+{changeVal}" : changeVal.ToString();
                }

                // 漲跌幅加上符號和%
                string changePctDisplay = changePercent;
                if (double.TryParse(changePercent, out double pctVal))
                {
                    changePctDisplay = pctVal >= 0 ? $"+{pctVal}%" : $"{pctVal}%";
                }

                // 計算價差
                string dayHighLowDiff = "";
                if (double.TryParse(dayHigh, out double highVal) && double.TryParse(dayLow, out double lowVal))
                {
                    dayHighLowDiff = (highVal - lowVal).ToString();
                }

                entities.Add(new DailyCloseEntity
                {
                    RankType = "Turnover",
                    SnapshotDate = tradingDate,
                    FetchTime = fetchTime,
                    Rank = rank,
                    Symbol = symbol,
                    Name = name,
                    Market = market,
                    Open = openPrice,
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
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 解析失敗：{line.Substring(0, Math.Min(50, line.Length))}... 錯誤：{ex.Message}");
            }
        }

        Console.WriteLine($"  成功解析 {entities.Count} 筆");

        if (entities.Count == 0)
        {
            Console.WriteLine("✗ 沒有資料可匯入");
            return;
        }

        // 顯示前5筆確認
        Console.WriteLine("\n  前5筆資料預覽：");
        foreach (var e in entities.Take(5))
        {
            Console.WriteLine($"    第{e.Rank}名 {e.Symbol} {e.Name} [{e.Market}] ${e.Price} {e.Change} {e.ChangePercent} 金額:{e.TurnoverHundredMillion}億");
        }

        // 存入資料庫
        using var db = new DatabaseContext();
        await db.Database.EnsureCreatedAsync();
        var repo = new StockRepository(db);

        // 檢查是否已有該日資料
        var existing = await repo.GetDailyCloseAsync(tradingDate);
        if (existing.Count > 0)
        {
            Console.WriteLine($"\n  ⚠ {tradingDate:yyyy/MM/dd} 已有 {existing.Count} 筆收盤資料，跳過匯入");
            Console.WriteLine("  如需重新匯入，請先在設定頁清除資料");
        }
        else
        {
            await repo.SaveDailyCloseAsync(entities);
            Console.WriteLine($"\n✓ 成功匯入 {entities.Count} 筆到 DailyCloseSnapshot（{tradingDate:yyyy/MM/dd}）");
        }

        Console.WriteLine("\n===== 匯入完成 =====");
    }
}
