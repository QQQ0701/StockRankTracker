using StockRankTracker.Contracts.IRepositories;
using StockRankTracker.Contracts.IServices;
using StockRankTracker.Helpers;
using StockRankTracker.Models.Crawlers;
using StockRankTracker.Models.Display;
using StockRankTracker.Models.Entities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace StockRankTracker.ViewModels;

/// <summary>
/// 盤後頁 ViewModel
/// 支援選擇日期和時間點，檢視歷史排名快照
/// </summary>
public class AfterMarketViewModel : INotifyPropertyChanged
{
    private readonly IStockRepository _repository;
    private readonly IDispatcherService _dispatcher;

    public AfterMarketViewModel(IStockRepository repository, IDispatcherService dispatcher)
    {
        _repository = repository;
        _dispatcher = dispatcher;
    }

    // ==================== 綁定屬性 ====================

    private ObservableCollection<StockDisplayModel> _stockList = new();
    /// <summary>股票排名顯示清單</summary>
    public ObservableCollection<StockDisplayModel> StockList
    {
        get => _stockList;
        set { _stockList = value; OnPropertyChanged(); }
    }

    private StatisticsModel _statistics = new();
    /// <summary>統計資訊</summary>
    public StatisticsModel Statistics
    {
        get => _statistics;
        set { _statistics = value; OnPropertyChanged(); }
    }

    private ObservableCollection<string> _availableDates = new();
    /// <summary>可選擇的日期清單</summary>
    public ObservableCollection<string> AvailableDates
    {
        get => _availableDates;
        set { _availableDates = value; OnPropertyChanged(); }
    }

    private ObservableCollection<string> _availableTimes = new();
    /// <summary>可選擇的時間清單</summary>
    public ObservableCollection<string> AvailableTimes
    {
        get => _availableTimes;
        set { _availableTimes = value; OnPropertyChanged(); }
    }

    private string? _selectedDate;
    /// <summary>目前選擇的日期</summary>
    public string? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate != value)
            {
                _selectedDate = value;
                OnPropertyChanged();
                _ = OnDateChangedAsync();
            }
        }
    }

    private string? _selectedTime;
    /// <summary>目前選擇的時間</summary>
    public string? SelectedTime
    {
        get => _selectedTime;
        set
        {
            if (_selectedTime != value)
            {
                _selectedTime = value;
                OnPropertyChanged();
                _ = OnTimeChangedAsync();
            }
        }
    }

    private bool _isFilterNewEntry;
    /// <summary>是否只顯示新上榜股票</summary>
    public bool IsFilterNewEntry
    {
        get => _isFilterNewEntry;
        set
        {
            _isFilterNewEntry = value;
            OnPropertyChanged();
            _ = RefreshDisplayListAsync();
        }
    }

    private int _totalCount;
    /// <summary>目前顯示的股票筆數</summary>
    public int TotalCount
    {
        get => _totalCount;
        set { _totalCount = value; OnPropertyChanged(); }
    }

    private string _compareBaseInfo = "";
    /// <summary>比對基準資訊（顯示比對的是哪一天）</summary>
    public string CompareBaseInfo
    {
        get => _compareBaseInfo;
        set { _compareBaseInfo = value; OnPropertyChanged(); }
    }

    // 暫存目前選中的快照資料
    private List<StockEntry> _currentStocks = new();
    private DateTime _currentSnapshotDate;

    // ==================== 方法 ====================

    /// <summary>
    /// 載入可用的日期清單（進入盤後頁時呼叫）
    /// 包含今天的盤中資料和歷史收盤資料，最新日期在前
    /// </summary>
    public async Task LoadAvailableDatesAsync()
    {
        try
        {
            var dates = new List<string>();

            // 加入今天（如果有盤中資料）
            var todaySessions = await _repository.GetIntradaySessionsAsync(DateTime.Today);
            if (todaySessions.Any(s => s.IsDataChanged))
            {
                dates.Add(DateTime.Today.ToString("yyyy/MM/dd") + " (今天)");
            }

            // 加入有收盤資料的日期
            var closeDates = await _repository.GetAvailableDailyCloseDatesAsync();
            foreach (var date in closeDates)
            {
                var label = date.ToString("yyyy/MM/dd");
                if (date.Date == DateTime.Today)
                {
                    if (!dates.Any(d => d.Contains("今天")))
                        dates.Add(label + " (今天)");
                }
                else
                {
                    dates.Add(label);
                }
            }

            // 去重並排序（最新的在前）
            dates = dates
                .Distinct()
                .OrderByDescending(d => d.Split(' ')[0])
                .ToList();

            _dispatcher.RunOnUI(() =>
            {
                AvailableDates.Clear();
                foreach (var d in dates)
                    AvailableDates.Add(d);

                if (AvailableDates.Count > 0 && SelectedDate == null)
                    SelectedDate = AvailableDates[0];
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadAvailableDatesAsync 錯誤：{ex.Message}");
        }
    }

    /// <summary>
    /// 日期變更時，載入該日期的可用時間點
    /// 今天：顯示所有盤中時間 + 收盤
    /// 其他日期：只顯示排程時間 + 收盤
    /// </summary>
    private async Task OnDateChangedAsync()
    {
        if (string.IsNullOrEmpty(SelectedDate)) return;

        try
        {
            var dateStr = SelectedDate.Split(' ')[0];
            var date = DateTime.Parse(dateStr);
            var times = new List<string>();

            bool isToday = date.Date == DateTime.Today;

            if (isToday)
            {
                var sessions = await _repository.GetIntradaySessionsAsync(date);
                foreach (var s in sessions.Where(s => s.IsDataChanged))
                {
                    times.Add(s.FetchTime.ToString("HH:mm"));
                }
            }
            else
            {
                // 非今天：只顯示排程抓取的盤中時間
                var sessions = await _repository.GetIntradaySessionsAsync(date);
                foreach (var s in sessions.Where(s => s.IsDataChanged && s.FetchSource == "Scheduled"))
                {
                    times.Add(s.FetchTime.ToString("HH:mm"));
                }
            }

            // 檢查是否有收盤資料
            var closeData = await _repository.GetDailyCloseAsync(date);
            if (closeData.Count > 0)
            {
                times.Add("13:35 (收盤)");
            }

            if (times.Count == 0)
            {
                times.Add("無資料");
            }

            _dispatcher.RunOnUI(() =>
            {
                AvailableTimes.Clear();
                foreach (var t in times)
                    AvailableTimes.Add(t);

                if (AvailableTimes.Count > 0)
                    SelectedTime = AvailableTimes.Last();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnDateChangedAsync 錯誤：{ex.Message}");
        }
    }

    /// <summary>
    /// 時間變更時，載入對應的快照資料
    /// </summary>
    private async Task OnTimeChangedAsync()
    {
        if (string.IsNullOrEmpty(SelectedDate) || string.IsNullOrEmpty(SelectedTime)) return;
        if (SelectedTime == "無資料") return;

        try
        {
            var dateStr = SelectedDate.Split(' ')[0];
            var date = DateTime.Parse(dateStr);
            _currentSnapshotDate = date;

            bool isCloseTime = SelectedTime.Contains("收盤");

            if (isCloseTime)
            {
                var closeData = await _repository.GetDailyCloseAsync(date);
                _currentStocks = closeData.Select(e => new StockEntry
                {
                    Rank = e.Rank,
                    Symbol = e.Symbol,
                    Name = e.Name,
                    Market = e.Market,
                    Open = e.Open,
                    Price = e.Price,
                    Change = e.Change,
                    ChangePercent = e.ChangePercent,
                    DayHigh = e.DayHigh,
                    DayLow = e.DayLow,
                    DayHighLowDiff = e.DayHighLowDiff,
                    VolK = e.VolK,
                    TurnoverHundredMillion = e.TurnoverHundredMillion
                }).ToList();
            }
            else
            {
                var sessions = await _repository.GetIntradaySessionsAsync(date);
                var timeStr = SelectedTime.Trim();
                var session = sessions.FirstOrDefault(s =>
                    s.IsDataChanged && s.FetchTime.ToString("HH:mm") == timeStr);

                if (session == null)
                {
                    _currentStocks.Clear();
                    await RefreshDisplayListAsync();
                    return;
                }

                var snapshots = await _repository.GetIntradaySnapshotAsync(session.SessionId);
                _currentStocks = snapshots.Select(e => new StockEntry
                {
                    Rank = e.Rank,
                    Symbol = e.Symbol,
                    Name = e.Name,
                    Market = e.Market,
                    Open = e.Open,
                    Price = e.Price,
                    Change = e.Change,
                    ChangePercent = e.ChangePercent,
                    DayHigh = e.DayHigh,
                    DayLow = e.DayLow,
                    DayHighLowDiff = e.DayHighLowDiff,
                    VolK = e.VolK,
                    TurnoverHundredMillion = e.TurnoverHundredMillion
                }).ToList();
            }

            if (_currentStocks.Count > 0)
            {
                Statistics = StockCalculateHelper.CalcStatistics(_currentStocks);
            }

            await RefreshDisplayListAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnTimeChangedAsync 錯誤：{ex.Message}");
        }
    }

    /// <summary>
    /// 重新整理顯示清單
    /// 比對基準：所選日期的前一個交易日 DailyClose
    /// 找不到則不比對，提示使用者補足歷史資料
    /// </summary>
    private async Task RefreshDisplayListAsync()
    {
        try
        {
            // 取得前一個交易日的收盤資料
            var previousClose = await _repository.GetPreviousDailyCloseAsync(_currentSnapshotDate);

            // 更新比對基準資訊
            if (previousClose.Count > 0)
            {
                var baseDate = previousClose[0].SnapshotDate;
                _dispatcher.RunOnUI(() =>
                    CompareBaseInfo = $"比對基準：{baseDate:MM/dd}（前一交易日收盤）");
            }
            else
            {
                _dispatcher.RunOnUI(() =>
                    CompareBaseInfo = "前一交易日無收盤資料，無法比對，請補足歷史資料");
            }

            var displayList = StockCalculateHelper.ToDisplayModels(_currentStocks, previousClose, 30);

            if (IsFilterNewEntry)
            {
                displayList = displayList.Where(s => s.IsNewEntry).ToList();
            }

            _dispatcher.RunOnUI(() =>
            {
                TotalCount = displayList.Count;
                StockList.Clear();
                foreach (var item in displayList)
                {
                    StockList.Add(item);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshDisplayListAsync 錯誤：{ex.Message}");
        }
    }

    // ==================== INotifyPropertyChanged ====================

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}