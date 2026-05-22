using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using StockRankTracker.Contracts.IRepositories;
using StockRankTracker.Contracts.IServices;
using StockRankTracker.Helpers;
using StockRankTracker.Services;

namespace StockRankTracker.ViewModels;

/// <summary>
/// 設定頁 ViewModel
/// 管理自訂抓取時間、資料庫容量、CSV 匯入、資料完整度檢查
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IStockRepository _repository;
    private readonly IDispatcherService _dispatcher;

    public SettingsViewModel(IStockRepository repository, IDispatcherService dispatcher)
    {
        _repository = repository;
        _dispatcher = dispatcher;

        SaveSettingsCommand = new RelayCommand(async () => await SaveSettingsAsync());
        CleanOldDataCommand = new RelayCommand(async () => await CleanOldDataAsync());
        ImportCsvCommand = new RelayCommand(async () => await ImportCsvAsync());

        SystemTimes = string.Join("、", SchedulerService.GetSystemTimeStrings());
    }

    // ==================== 綁定屬性 ====================

    /// <summary>系統固定時間（唯讀顯示）</summary>
    public string SystemTimes { get; }

    private string _customTime1 = "";
    public string CustomTime1
    {
        get => _customTime1;
        set { _customTime1 = value; OnPropertyChanged(); }
    }

    private string _customTime2 = "";
    public string CustomTime2
    {
        get => _customTime2;
        set { _customTime2 = value; OnPropertyChanged(); }
    }

    private string _customTime3 = "";
    public string CustomTime3
    {
        get => _customTime3;
        set { _customTime3 = value; OnPropertyChanged(); }
    }

    private string _customTime4 = "";
    public string CustomTime4
    {
        get => _customTime4;
        set { _customTime4 = value; OnPropertyChanged(); }
    }

    private string _customTime5 = "";
    public string CustomTime5
    {
        get => _customTime5;
        set { _customTime5 = value; OnPropertyChanged(); }
    }

    private double _databaseSizeMB;
    public double DatabaseSizeMB
    {
        get => _databaseSizeMB;
        set { _databaseSizeMB = value; OnPropertyChanged(); OnPropertyChanged(nameof(DatabaseSizeDisplay)); }
    }

    public string DatabaseSizeDisplay => $"{DatabaseSizeMB:F2} MB";

    private int _warningSizeMB = 500;
    public int WarningSizeMB
    {
        get => _warningSizeMB;
        set { _warningSizeMB = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    private string _statusColor = "#888";
    public string StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(); }
    }

    private string _importResult = "";
    public string ImportResult
    {
        get => _importResult;
        set { _importResult = value; OnPropertyChanged(); }
    }

    private ObservableCollection<DataDateInfo> _dataDateList = new();
    public ObservableCollection<DataDateInfo> DataDateList
    {
        get => _dataDateList;
        set { _dataDateList = value; OnPropertyChanged(); }
    }

    // ==================== 命令 ====================

    public ICommand SaveSettingsCommand { get; }
    public ICommand CleanOldDataCommand { get; }
    public ICommand ImportCsvCommand { get; }

    // ==================== 方法 ====================

    /// <summary>
    /// 載入設定（進入設定頁時呼叫）
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        try
        {
            CustomTime1 = await _repository.GetSettingAsync("CustomTime1") ?? "";
            CustomTime2 = await _repository.GetSettingAsync("CustomTime2") ?? "";
            CustomTime3 = await _repository.GetSettingAsync("CustomTime3") ?? "";
            CustomTime4 = await _repository.GetSettingAsync("CustomTime4") ?? "";
            CustomTime5 = await _repository.GetSettingAsync("CustomTime5") ?? "";

            var warningStr = await _repository.GetSettingAsync("StorageWarningSizeMB");
            if (int.TryParse(warningStr, out int warningSize))
                WarningSizeMB = warningSize;

            DatabaseSizeMB = await _repository.GetDatabaseSizeAsync();
            StatusMessage = "";
            ImportResult = "";

            await LoadDataCompleteness();
        }
        catch (Exception ex)
        {
            ShowStatus($"載入設定失敗：{ex.Message}", false);
        }
    }

    /// <summary>
    /// 匯入 CSV 檔案（支援多選）
    /// 匯入的資料標記 DataSource 為 "Import"
    /// </summary>
    private async Task ImportCsvAsync()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "選擇 XQ 匯出的 CSV 檔案",
                Filter = "CSV 檔案 (*.csv)|*.csv",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
                return;

            var results = new List<string>();
            int successCount = 0;
            int skipCount = 0;
            int failCount = 0;

            foreach (var filePath in dialog.FileNames)
            {
                var parseResult = CsvImportHelper.ParseCsv(filePath);

                if (!parseResult.Success)
                {
                    results.Add($"✗ {parseResult.FileName}：{parseResult.ErrorMessage}");
                    failCount++;
                    continue;
                }

                // 檢查是否已有該日資料（透過 Repository 的防重複機制）
                if (await _repository.HasDailyCloseAsync(parseResult.TradingDate))
                {
                    results.Add($"⊘ {parseResult.FileName}：{parseResult.TradingDate:MM/dd} 已有資料，跳過");
                    skipCount++;
                    continue;
                }

                // 標記資料來源為匯入
                foreach (var e in parseResult.Entities)
                    e.DataSource = "Import";

                await _repository.SaveDailyCloseAsync(parseResult.Entities);
                results.Add($"✓ {parseResult.FileName}：{parseResult.TradingDate:MM/dd} 匯入 {parseResult.Entities.Count} 筆");
                successCount++;
            }

            var summary = $"匯入完成：成功 {successCount} 天";
            if (skipCount > 0) summary += $"、跳過 {skipCount} 天";
            if (failCount > 0) summary += $"、失敗 {failCount} 天";

            ImportResult = summary + "\n" + string.Join("\n", results);

            DatabaseSizeMB = await _repository.GetDatabaseSizeAsync();
            await LoadDataCompleteness();
        }
        catch (Exception ex)
        {
            ImportResult = $"匯入失敗：{ex.Message}";
        }
    }

    /// <summary>
    /// 載入資料完整度（最近20個交易日的收盤資料有無）
    /// </summary>
    private async Task LoadDataCompleteness()
    {
        try
        {
            var holidays = DateTimeHelper.GetHolidays(DateTime.Now.Year);
            var availableDates = await _repository.GetAvailableDailyCloseDatesAsync();
            var availableSet = new HashSet<DateTime>(availableDates.Select(d => d.Date));

            var dateList = new List<DataDateInfo>();

            for (int i = 0; i < 45; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                if (date.Date >= DateTime.Today) continue;
                if (!DateTimeHelper.IsTradingDay(date, holidays)) continue;

                dateList.Add(new DataDateInfo
                {
                    Date = date,
                    DateDisplay = date.ToString("MM/dd (ddd)"),
                    HasData = availableSet.Contains(date.Date),
                    StatusDisplay = availableSet.Contains(date.Date) ? "✓" : "缺少",
                    StatusColor = availableSet.Contains(date.Date) ? "#4DD078" : "#FF5050"
                });

                if (dateList.Count >= 20) break;
            }

            _dispatcher.RunOnUI(() =>
            {
                DataDateList.Clear();
                foreach (var item in dateList)
                    DataDateList.Add(item);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadDataCompleteness 錯誤：{ex.Message}");
        }
    }

    /// <summary>
    /// 儲存設定（驗證自訂時間格式和範圍）
    /// </summary>
    private async Task SaveSettingsAsync()
    {
        try
        {
            var customTimes = new[] { CustomTime1, CustomTime2, CustomTime3, CustomTime4, CustomTime5 };
            var systemTimeStrings = SchedulerService.GetSystemTimeStrings();
            var validTimes = new List<string>();

            for (int i = 0; i < customTimes.Length; i++)
            {
                var time = customTimes[i].Trim();
                if (string.IsNullOrEmpty(time)) continue;

                if (!TimeSpan.TryParse(time, out var ts))
                {
                    ShowStatus($"自訂時間{i + 1}「{time}」格式不正確，請使用 HH:mm 格式", false);
                    return;
                }

                if (ts < new TimeSpan(9, 0, 0) || ts > new TimeSpan(13, 30, 0))
                {
                    ShowStatus($"自訂時間{i + 1}「{time}」超出範圍（09:00 ~ 13:30）", false);
                    return;
                }

                if (systemTimeStrings.Contains(time))
                {
                    ShowStatus($"自訂時間{i + 1}「{time}」與系統固定時間重複", false);
                    return;
                }

                if (validTimes.Contains(time))
                {
                    ShowStatus($"自訂時間{i + 1}「{time}」與其他自訂時間重複", false);
                    return;
                }

                validTimes.Add(time);
            }

            await _repository.SetSettingAsync("CustomTime1", CustomTime1.Trim());
            await _repository.SetSettingAsync("CustomTime2", CustomTime2.Trim());
            await _repository.SetSettingAsync("CustomTime3", CustomTime3.Trim());
            await _repository.SetSettingAsync("CustomTime4", CustomTime4.Trim());
            await _repository.SetSettingAsync("CustomTime5", CustomTime5.Trim());
            await _repository.SetSettingAsync("StorageWarningSizeMB", WarningSizeMB.ToString());

            ShowStatus("設定已儲存", true);
        }
        catch (Exception ex)
        {
            ShowStatus($"儲存失敗：{ex.Message}", false);
        }
    }

    /// <summary>
    /// 清除30天以前的收盤資料和盤中舊資料
    /// </summary>
    private async Task CleanOldDataAsync()
    {
        try
        {
            await _repository.DeleteOldDailyCloseAsync(30);
            await _repository.CleanIntradayDataAsync(30);
            DatabaseSizeMB = await _repository.GetDatabaseSizeAsync();
            await LoadDataCompleteness();
            ShowStatus("已清除30天以前的收盤資料和盤中舊資料", true);
        }
        catch (Exception ex)
        {
            ShowStatus($"清除失敗：{ex.Message}", false);
        }
    }

    /// <summary>
    /// 顯示狀態訊息（綠色=成功，紅色=失敗）
    /// </summary>
    private void ShowStatus(string message, bool isSuccess)
    {
        StatusMessage = message;
        StatusColor = isSuccess ? "#4DD078" : "#FF5050";
    }

    // ==================== INotifyPropertyChanged ====================

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// 資料完整度項目（供設定頁 DataGrid 顯示）
/// </summary>
public class DataDateInfo
{
    public DateTime Date { get; set; }
    public string DateDisplay { get; set; } = "";
    public bool HasData { get; set; }
    public string StatusDisplay { get; set; } = "";
    public string StatusColor { get; set; } = "";
}