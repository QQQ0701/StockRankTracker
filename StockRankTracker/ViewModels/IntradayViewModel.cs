using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StockRankTracker.Contracts.IRepositories;
using StockRankTracker.Contracts.IServices;
using StockRankTracker.Helpers;
using StockRankTracker.Models.Crawlers;
using StockRankTracker.Models.Display;

namespace StockRankTracker.ViewModels;

/// <summary>
/// 盤中頁 ViewModel
/// 顯示最新的盤中排名資料，支援手動抓取和自動更新
/// </summary>
public class IntradayViewModel : INotifyPropertyChanged
{
    private readonly IFetchOrchestrator _orchestrator;
    private readonly ISchedulerService _scheduler;
    private readonly IStockRepository _repository;
    private readonly IDispatcherService _dispatcher;

    // 完整的100名爬蟲資料（用來算統計和轉換顯示）
    private List<StockEntry> _allStocks = new();

    public IntradayViewModel(
        IFetchOrchestrator orchestrator,
        ISchedulerService scheduler,
        IStockRepository repository,
        IDispatcherService dispatcher)
    {
        _orchestrator = orchestrator;
        _scheduler = scheduler;
        _repository = repository;
        _dispatcher = dispatcher;

        FetchCommand = new RelayCommand(async () => await ManualFetchAsync(), () => !IsFetching);

        // 監聽抓取完成事件
        _orchestrator.OnFetchCompleted += async () =>
        {
            await LoadLatestDataAsync();
        };

        // 監聽抓取狀態變化
        _scheduler.OnFetchingStateChanged += (isFetching) =>
        {
            _dispatcher.RunOnUI(() => IsFetching = isFetching);
        };

        // 監聽容量警示
        _orchestrator.OnStorageWarning += (sizeMB) =>
        {
            _dispatcher.RunOnUI(() => StorageWarning = $"⚠ 資料庫已達 {sizeMB:F1} MB，請至設定頁清除舊資料");
        };

        // 監聽使用者訊息（靜止期提示等）
        _orchestrator.OnUserMessage += (message) =>
        {
            _dispatcher.RunOnUI(() => UserMessage = message);
        };
    }

    // ==================== 綁定屬性 ====================

    private ObservableCollection<StockDisplayModel> _stockList = new();
    /// <summary>股票排名顯示清單（前30名）</summary>
    public ObservableCollection<StockDisplayModel> StockList
    {
        get => _stockList;
        set { _stockList = value; OnPropertyChanged(); }
    }

    private StatisticsModel _statistics = new();
    /// <summary>統計資訊（漲跌家數等）</summary>
    public StatisticsModel Statistics
    {
        get => _statistics;
        set { _statistics = value; OnPropertyChanged(); }
    }

    private string _lastFetchTime = "--:--";
    /// <summary>最後抓取時間顯示文字</summary>
    public string LastFetchTime
    {
        get => _lastFetchTime;
        set { _lastFetchTime = value; OnPropertyChanged(); }
    }

    private bool _isFetching;
    /// <summary>是否正在抓取中（控制按鈕狀態）</summary>
    public bool IsFetching
    {
        get => _isFetching;
        set
        {
            _isFetching = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanFetch));
            (FetchCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>是否可以抓取（IsFetching 的反轉）</summary>
    public bool CanFetch => !IsFetching;

    private bool _isFilterNewEntry;
    /// <summary>是否只顯示新上榜股票</summary>
    public bool IsFilterNewEntry
    {
        get => _isFilterNewEntry;
        set
        {
            _isFilterNewEntry = value;
            OnPropertyChanged();
            RefreshDisplayList();
        }
    }

    private int _totalCount;
    /// <summary>目前顯示的股票筆數</summary>
    public int TotalCount
    {
        get => _totalCount;
        set { _totalCount = value; OnPropertyChanged(); }
    }

    private string _storageWarning = "";
    /// <summary>資料庫容量警示訊息</summary>
    public string StorageWarning
    {
        get => _storageWarning;
        set { _storageWarning = value; OnPropertyChanged(); }
    }

    private string _userMessage = "";
    /// <summary>使用者提示訊息（靜止期、資料已存在等）</summary>
    public string UserMessage
    {
        get => _userMessage;
        set { _userMessage = value; OnPropertyChanged(); }
    }

    private string _compareBaseInfo = "";
    /// <summary>比對基準資訊（顯示比對的是哪一天）</summary>
    public string CompareBaseInfo
    {
        get => _compareBaseInfo;
        set { _compareBaseInfo = value; OnPropertyChanged(); }
    }

    // ==================== 命令 ====================

    /// <summary>手動抓取命令</summary>
    public ICommand FetchCommand { get; }

    // ==================== 方法 ====================

    /// <summary>
    /// 手動抓取（使用者按按鈕，FetchSource 為 "Manual"）
    /// </summary>
    private async Task ManualFetchAsync()
    {
        UserMessage = ""; // 清除之前的提示
        await _scheduler.ExecuteFetchAsync("Manual");
    }

    /// <summary>
    /// 載入最新的盤中資料並更新 UI
    /// </summary>
    public async Task LoadLatestDataAsync()
    {
        try
        {
            var sessions = await _repository.GetIntradaySessionsAsync(DateTime.Today);
            if (sessions.Count == 0) return;

            var lastChangedSession = sessions.LastOrDefault(s => s.IsDataChanged);
            if (lastChangedSession == null) return;

            var snapshots = await _repository.GetIntradaySnapshotAsync(lastChangedSession.SessionId);
            if (snapshots.Count == 0) return;

            _allStocks = snapshots.Select(s => new StockEntry
            {
                Rank = s.Rank,
                Symbol = s.Symbol,
                Name = s.Name,
                Market = s.Market,
                Open = s.Open,
                Price = s.Price,
                Change = s.Change,
                ChangePercent = s.ChangePercent,
                DayHigh = s.DayHigh,
                DayLow = s.DayLow,
                DayHighLowDiff = s.DayHighLowDiff,
                VolK = s.VolK,
                TurnoverHundredMillion = s.TurnoverHundredMillion
            }).ToList();

            var stats = StockCalculateHelper.CalcStatistics(_allStocks);
            var timeStr = lastChangedSession.FetchTime.ToString("HH:mm");

            _dispatcher.RunOnUI(() =>
            {
                Statistics = stats;
                LastFetchTime = timeStr;
            });

            RefreshDisplayList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadLatestDataAsync 錯誤：{ex.Message}");
        }
    }

    /// <summary>
    /// 重新整理顯示清單（套用篩選和比對）
    /// 比對基準：前一個交易日的 DailyClose，找不到則不比對
    /// </summary>
    private async void RefreshDisplayList()
    {
        try
        {
            // 取得前一個交易日的收盤資料作為比對基準
            var previousClose = await _repository.GetPreviousDailyCloseAsync(DateTime.Today);

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

            // 轉換為 DisplayModel（前30名）
            var displayList = StockCalculateHelper.ToDisplayModels(_allStocks, previousClose, 30);

            // 篩選：只看新上榜
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
            System.Diagnostics.Debug.WriteLine($"RefreshDisplayList 錯誤：{ex.Message}");
        }
    }

    // ==================== INotifyPropertyChanged ====================

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}