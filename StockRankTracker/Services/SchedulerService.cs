using StockRankTracker.Contracts.IRepositories;
using StockRankTracker.Contracts.IServices;
using StockRankTracker.Helpers;

namespace StockRankTracker.Services;

/// <summary>
/// 排程服務（實作 ISchedulerService）
/// 每30秒檢查是否需要抓取，管理排程時間和手動觸發
/// </summary>
public class SchedulerService : ISchedulerService
{
    private readonly IFetchOrchestrator _orchestrator;
    private readonly IStockRepository _repository;
    private Timer? _timer;
    private bool _isRunning;

    // 用 lock 保護共用狀態，避免 Timer 執行緒和 UI 執行緒同時存取
    private readonly object _lock = new();

    /// <summary>是否正在抓取中（供 UI 控制按鈕反白）</summary>
    public bool IsFetching { get; private set; }

    /// <summary>抓取狀態變化事件（通知 UI 更新按鈕狀態）</summary>
    public event Action<bool>? OnFetchingStateChanged;

    // 系統固定排程時間
    private static readonly List<TimeSpan> SystemTimes = new()
    {
        new TimeSpan(9, 1, 0),   // 09:01
        new TimeSpan(9, 6, 0),   // 09:06
        new TimeSpan(9, 15, 0),  // 09:15
        new TimeSpan(9, 30, 0),  // 09:30
        new TimeSpan(10, 0, 0),  // 10:00
        new TimeSpan(13, 35, 0), // 13:35（收盤）
    };

    // 已觸發的時間點（避免同一分鐘重複觸發）
    private readonly HashSet<string> _triggeredToday = new();
    private DateTime _lastTriggerDate = DateTime.MinValue;

    public SchedulerService(IFetchOrchestrator orchestrator, IStockRepository repository)
    {
        _orchestrator = orchestrator;
        _repository = repository;
    }

    /// <summary>
    /// 啟動排程（每30秒檢查一次）
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _timer = new Timer(CheckSchedule, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// 停止排程
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Timer 回呼：檢查是否符合任何排程時間
    /// 用 try-catch 包住整段邏輯，避免 async void 例外導致 crash
    /// </summary>
    private async void CheckSchedule(object? state)
    {
        try
        {
            // 用 lock 檢查並設定 IsFetching，避免 race condition
            lock (_lock)
            {
                if (IsFetching) return;
            }

            var now = DateTime.Now;

            // 跨日重置已觸發清單
            lock (_lock)
            {
                if (now.Date != _lastTriggerDate)
                {
                    _triggeredToday.Clear();
                    _lastTriggerDate = now.Date;
                }
            }

            // 假日不抓取
            if (DateTimeHelper.IsWeekend(now))
                return;

            var holidays = DateTimeHelper.GetHolidays(now.Year);
            if (DateTimeHelper.IsHoliday(now, holidays))
                return;

            // 取得自訂時間
            var customTimes = await GetCustomTimesAsync();

            // 合併所有排程時間
            var allTimes = new List<TimeSpan>(SystemTimes);
            allTimes.AddRange(customTimes);

            // 檢查現在是否符合任何排程時間
            var currentTime = now.TimeOfDay;
            foreach (var scheduleTime in allTimes)
            {
                var timeKey = scheduleTime.ToString(@"hh\:mm");

                lock (_lock)
                {
                    if (_triggeredToday.Contains(timeKey))
                        continue;
                }

                // 在排程時間的前後45秒內觸發
                var diff = Math.Abs((currentTime - scheduleTime).TotalSeconds);
                if (diff <= 45)
                {
                    lock (_lock)
                    {
                        _triggeredToday.Add(timeKey);
                    }

                    await ExecuteFetchAsync("Scheduled");
                    break; // 一次只觸發一個
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckSchedule 錯誤：{ex.Message}");
        }
    }

    /// <summary>
    /// 執行抓取（供排程和手動按鈕共用）
    /// </summary>
    /// <param name="fetchSource">抓取來源："Scheduled" 或 "Manual"</param>
    public async Task ExecuteFetchAsync(string fetchSource = "Manual")
    {
        lock (_lock)
        {
            if (IsFetching) return;
            IsFetching = true;
        }

        try
        {
            OnFetchingStateChanged?.Invoke(true);
            await _orchestrator.FetchAndSaveAsync(fetchSource);
        }
        finally
        {
            lock (_lock)
            {
                IsFetching = false;
            }
            OnFetchingStateChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// 從設定讀取自訂抓取時間（最多5組）
    /// </summary>
    private async Task<List<TimeSpan>> GetCustomTimesAsync()
    {
        var times = new List<TimeSpan>();

        for (int i = 1; i <= 5; i++)
        {
            var value = await _repository.GetSettingAsync($"CustomTime{i}");
            if (!string.IsNullOrWhiteSpace(value) && TimeSpan.TryParse(value, out var time))
            {
                times.Add(time);
            }
        }

        return times;
    }

    /// <summary>
    /// 取得系統固定時間清單（供設定頁唯讀顯示）
    /// </summary>
    public static List<string> GetSystemTimeStrings()
    {
        return SystemTimes.Select(t => t.ToString(@"hh\:mm")).ToList();
    }
}