using StockRankTracker.Cloud.Models;
using System.Text.RegularExpressions;

namespace StockRankTracker.Cloud.Services;

/// <summary>
/// 成交金額排行爬蟲（雲端版，移除 DI 依賴）
/// </summary>>
public class TurnoverCrawler 
{
    private const string Url = "https://tw.stock.yahoo.com/rank/turnover";
    private static readonly HttpClient _client = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36" }
        }
    };

    public async Task<List<StockEntry>> FetchAsync(int limit = 100)
    {
        var html = await _client.GetStringAsync(Url);

        // 抓每個股票物件（從 "rank":"N" 開始到下一個物件結束）
        var objPattern = new Regex(
            @"\{[^{}]*""rank"":""(\d+)""[^{}]*\}",
            RegexOptions.Singleline);

        var fieldPattern = new Regex(
            @"""change"":""(?<change>[^""]*)""|" +
            @"""changePercent"":""(?<changePercent>[^""]*)""|" +
            @"""dayHigh"":""(?<dayHigh>[^""]*)""|" +
            @"""dayHighLowDiff"":""(?<dayHighLowDiff>[^""]*)""|" +
            @"""dayLow"":""(?<dayLow>[^""]*)""|" +
            @"""name"":""(?<name>[^""]*)""|" +
            @"""price"":""(?<price>[^""]*)""|" +
            @"""symbol"":""(?<symbol>[^""]*)""|" +
            @"""turnoverK"":""(?<turnoverK>[^""]*)""|" +
            @"""volK"":(?<volK>\d+)");

        var seen = new HashSet<string>();
        var stocks = new List<StockEntry>();

        foreach (Match obj in objPattern.Matches(html))
        {
            int rank = int.Parse(obj.Groups[1].Value);
            if (rank > limit) continue;

            var f = fieldPattern.Matches(obj.Value);
            var d = new Dictionary<string, string>();
            foreach (Match m in f)
                foreach (Group g in m.Groups.Values)
                    if (g.Name != "0" && g.Success && !d.ContainsKey(g.Name))
                        d[g.Name] = g.Value;

            if (!d.TryGetValue("symbol", out var rawSymbol) || !seen.Add(rawSymbol))
                continue;

            // 拆分 Market（例如 "2330.TW" → Symbol="2330", Market="TW"）
            string symbol = rawSymbol;
            string market = "";
            if (rawSymbol.Contains('.'))
            {
                var parts = rawSymbol.Split('.');
                symbol = parts[0];
                market = parts[1]; // TW 或 TWO
            }

            // 成交額轉換：turnoverK（千元）→ 億元
            string turnoverBillion = "";
            if (d.TryGetValue("turnoverK", out var tk) && long.TryParse(tk, out long tkVal))
                turnoverBillion = (tkVal / 100000.0).ToString("F2");

            // 量(張)
            string volStr = d.TryGetValue("volK", out var vk) ? vk : "";

            // 取得最低價（暫時作為開盤價替代）
            string dayLow = d.GetValueOrDefault("dayLow", "");

            stocks.Add(new StockEntry
            {
                Rank = rank,
                Symbol = symbol,
                Name = d.GetValueOrDefault("name", ""),
                Market = market,
                Open = dayLow,  // 暫用最低價替代，未來接其他來源
                Price = d.GetValueOrDefault("price", ""),
                Change = d.GetValueOrDefault("change", ""),
                ChangePercent = d.GetValueOrDefault("changePercent", ""),
                DayHigh = d.GetValueOrDefault("dayHigh", ""),
                DayLow = dayLow,
                DayHighLowDiff = d.GetValueOrDefault("dayHighLowDiff", ""),
                VolK = volStr,
                TurnoverHundredMillion = turnoverBillion
            });
        }

        return stocks.OrderBy(s => s.Rank).ToList();
    }
}
