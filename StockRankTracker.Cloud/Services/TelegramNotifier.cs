using System.Net.Http;
using System.Text;
using System.Text.Json;
using StockRankTracker.Cloud.Models;

namespace StockRankTracker.Cloud.Services;

/// <summary>
/// Telegram 推播通知
/// </summary>
public class TelegramNotifier
{
    private static readonly HttpClient _client = new();
    private readonly string _botToken;
    private readonly string _chatId;

    public TelegramNotifier()
    {
        _botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
                    ?? throw new Exception("未設定 TELEGRAM_BOT_TOKEN 環境變數");
        _chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")
                  ?? throw new Exception("未設定 TELEGRAM_CHAT_ID 環境變數");
    }

    public async Task SendAsync(DateTime now, List<StockEntry> newEntries)
    {
        var timeStr = now.ToString("HH:mm");
        var sb = new StringBuilder();
        sb.AppendLine($"📊 盤中排名更新（{timeStr}）");
        sb.AppendLine();
        sb.AppendLine("🆕 新上榜：");

        for (int i = 0; i < newEntries.Count; i++)
        {
            var s = newEntries[i];
            var arrow = "";
            if (decimal.TryParse(s.ChangePercent, out var pct))
                arrow = pct >= 0 ? "▲" : "▼";

            sb.AppendLine($"{i + 1}. #{s.Rank} {s.Name}({s.Symbol}) {s.Price} {arrow}{s.ChangePercent}");
        }

        var message = sb.ToString();
        var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

        var payload = new
        {
            chat_id = _chatId,
            text = message,
            parse_mode = "HTML"
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Telegram 發送失敗：{response.StatusCode} - {body}");
        }
    }
}
