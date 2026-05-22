using StockRankTracker.Contracts.ICrawlers;
using StockRankTracker.Contracts.IRepositories;
using StockRankTracker.Contracts.IServices;
using StockRankTracker.Helpers;
using StockRankTracker.Models.Entities;

namespace StockRankTracker.Services;

/// <summary>
/// 抓取流程協調（實作 IFetchOrchestrator）
/// 根據當前時段判斷走盤中、收盤、補抓或不處理
/// </summary>
public class FetchOrchestrator : IFetchOrchestrator
{
    private readonly IRankCrawler _crawler;
    private readonly IStockRepository _repository;

    /// <summary>最後一次抓取時間（供 UI 顯示）</summary>
    public DateTime? LastFetchTime { get; private set; }

    /// <summary>最後一次抓取是否成功</summary>
    public bool LastFetchSuccess { get; private set; }

    /// <summary>最後一次錯誤訊息</summary>
    public string LastErrorMessage { get; private set; } = "";

    /// <summary>抓取完成事件（通知 ViewModel 更新 UI）</summary>
    public event Action? OnFetchCompleted;

    /// <summary>資料庫容量超過警示門檻事件</summary>
    public event Action<double>? OnStorageWarning;

    /// <summary>需要提示使用者的訊息（例如靜止期、不處理等）</summary>
    public event Action<string>? OnUserMessage;

    public FetchOrchestrator(IRankCrawler crawler, IStockRepository repository)
    {
        _crawler = crawler;
        _repository = repository;
    }

    /// <summary>
    /// 主要抓取流程入口
    /// 根據當前時段決定走盤中、收盤、補抓或不處理
    /// </summary>
    /// <param name="fetchSource">抓取來源："Scheduled" 或 "Manual"</param>
    public async Task FetchAndSaveAsync(string fetchSource = "Scheduled")
    {
        try
        {
            var now = DateTime.Now;

            // 非交易日不處理（假日、週末）
            if (!IsTradingDay(now))
            {
                OnUserMessage?.Invoke("今天非交易日，不進行抓取");
                return;
            }

            // 判斷目前時段
            var period = GetCurrentPeriod(now);

            switch (period)
            {
                case TradingPeriod.Intraday:
                    await HandleIntradayAsync(now, fetchSource);
                    break;

                case TradingPeriod.Settling:
                    OnUserMessage?.Invoke("收盤結算中（13:30~13:35），請稍候");
                    return;

                case TradingPeriod.Close:
                    await HandleCloseAsync(now, fetchSource);
                    break;

                case TradingPeriod.AfterClose:
                    await HandleAfterCloseAsync(now);
                    break;

                case TradingPeriod.BeforeOpen:
                    await HandleBeforeOpenAsync(now);
                    break;
            }

            // 檢查資料庫容量
            await CheckStorageWarningAsync();
        }
        catch (Exception ex)
        {
            LastFetchSuccess = false;
            LastErrorMessage = ex.Message;
        }
        finally
        {
            OnFetchCompleted?.Invoke();
        }
    }

    // ==================== 時段判斷 ====================

    /// <summary>
    /// 交易時段列舉
    /// </summary>
    private enum TradingPeriod
    {
        BeforeOpen,  // 00:00~09:00
        Intraday,    // 09:00~13:30
        Settling,    // 13:30~13:35
        Close,       // 13:35（排程觸發）
        AfterClose   // 13:35~00:00（手動觸發）
    }

    /// <summary>
    /// 根據當前時間判斷所處的交易時段
    /// </summary>
    private TradingPeriod GetCurrentPeriod(DateTime now)
    {
        var time = now.TimeOfDay;

        if (time < new TimeSpan(9, 0, 0))
            return TradingPeriod.BeforeOpen;

        if (time < new TimeSpan(13, 30, 0))
            return TradingPeriod.Intraday;

        if (time < new TimeSpan(13, 35, 0))
            return TradingPeriod.Settling;

        return TradingPeriod.AfterClose;
    }

    /// <summary>
    /// 判斷指定日期是否為交易日（非週末、非國定假日）
    /// </summary>
    private bool IsTradingDay(DateTime date)
    {
        if (DateTimeHelper.IsWeekend(date))
            return false;

        var holidays = DateTimeHelper.GetHolidays(date.Year);
        return !DateTimeHelper.IsHoliday(date, holidays);
    }

    // ==================== 各時段處理邏輯 ====================

    /// <summary>
    /// 盤中流程（09:00~13:30）
    /// 抓取資料後比對上一筆快照，有變化才存入 Snapshot
    /// </summary>
    private async Task HandleIntradayAsync(DateTime now, string fetchSource)
    {
        var tradingDate = now.Date;

        var stocks = await FetchStocksAsync();
        if (stocks == null) return;

        // 第一次抓取時，清除舊的盤中資料
        var existingSessions = await _repository.GetIntradaySessionsAsync(tradingDate);
        if (existingSessions.Count == 0)
        {
            await _repository.CleanIntradayDataAsync();
        }

        // 取得上一筆快照來比對
        List<IntradaySnapshotEntity>? previousSnapshot = null;
        if (existingSessions.Count > 0)
        {
            var lastSession = existingSessions.Last();
            previousSnapshot = await _repository.GetIntradaySnapshotAsync(lastSession.SessionId);
        }

        // 比對是否有變化
        bool isChanged = DatabaseHelper.IsDataChanged(stocks, previousSnapshot);

        // 寫入 Session 記錄（不管有沒有變化都記錄）
        var session = new IntradaySessionEntity
        {
            RankType = "Turnover",
            FetchTime = now,
            TradingDate = tradingDate,
            IsDataChanged = isChanged,
            FetchSource = fetchSource
        };
        var sessionId = await _repository.SaveIntradaySessionAsync(session);

        // 有變化才寫入 Snapshot
        if (isChanged)
        {
            var snapshots = DatabaseHelper.ToIntradaySnapshotEntities(stocks, sessionId);
            await _repository.SaveIntradaySnapshotAsync(snapshots);
        }

        LastFetchTime = now;
        LastFetchSuccess = true;
        LastErrorMessage = "";
    }

    /// <summary>
    /// 收盤流程（13:35 排程觸發）
    /// 抓取資料直接存入 DailyClose，該日已有則跳過
    /// </summary>
    private async Task HandleCloseAsync(DateTime now, string fetchSource)
    {
        var tradingDate = now.Date;

        // 已有收盤資料就跳過
        if (await _repository.HasDailyCloseAsync(tradingDate))
        {
            OnUserMessage?.Invoke("今日收盤資料已存在，跳過抓取");
            return;
        }

        var stocks = await FetchStocksAsync();
        if (stocks == null) return;

        var entities = DatabaseHelper.ToDailyCloseEntities(stocks, tradingDate, now);

        // 標記資料來源
        foreach (var e in entities)
            e.DataSource = "Fetch";

        await _repository.SaveDailyCloseAsync(entities);

        LastFetchTime = now;
        LastFetchSuccess = true;
        LastErrorMessage = "";
    }

    /// <summary>
    /// 收盤後流程（13:35~00:00 手動觸發）
    /// 如果當天還沒有收盤資料就補存，已有則跳過
    /// </summary>
    private async Task HandleAfterCloseAsync(DateTime now)
    {
        var tradingDate = now.Date;

        if (await _repository.HasDailyCloseAsync(tradingDate))
        {
            OnUserMessage?.Invoke("今日收盤資料已存在，無需重複抓取");
            return;
        }

        var stocks = await FetchStocksAsync();
        if (stocks == null) return;

        var entities = DatabaseHelper.ToDailyCloseEntities(stocks, tradingDate, now);

        foreach (var e in entities)
            e.DataSource = "AutoFill";

        await _repository.SaveDailyCloseAsync(entities);

        LastFetchTime = now;
        LastFetchSuccess = true;
        LastErrorMessage = "";
    }

    /// <summary>
    /// 隔日開盤前流程（00:00~09:00 手動或啟動時觸發）
    /// 如果前一個交易日沒有收盤資料就補存
    /// </summary>
    private async Task HandleBeforeOpenAsync(DateTime now)
    {
        var previousTradingDate = DateTimeHelper.GetPreviousTradingDate(now.Date);

        if (await _repository.HasDailyCloseAsync(previousTradingDate))
        {
            OnUserMessage?.Invoke("前一交易日收盤資料已存在，無需抓取");
            return;
        }

        var stocks = await FetchStocksAsync();
        if (stocks == null) return;

        var entities = DatabaseHelper.ToDailyCloseEntities(stocks, previousTradingDate, now);

        foreach (var e in entities)
            e.DataSource = "AutoFill";

        await _repository.SaveDailyCloseAsync(entities);

        LastFetchTime = now;
        LastFetchSuccess = true;
        LastErrorMessage = "";
    }

    // ==================== 共用方法 ====================

    /// <summary>
    /// 執行爬蟲抓取100名資料，失敗時設定錯誤狀態並回傳 null
    /// </summary>
    private async Task<List<Models.Crawlers.StockEntry>?> FetchStocksAsync()
    {
        var stocks = await _crawler.FetchAsync(100);

        if (stocks.Count == 0)
        {
            LastFetchSuccess = false;
            LastErrorMessage = "抓取結果為空";
            return null;
        }

        return stocks;
    }

    /// <summary>
    /// 檢查資料庫容量是否超過警示門檻
    /// </summary>
    private async Task CheckStorageWarningAsync()
    {
        try
        {
            var sizeMB = await _repository.GetDatabaseSizeAsync();
            var warningStr = await _repository.GetSettingAsync("StorageWarningSizeMB");
            var warningMB = 500;
            if (int.TryParse(warningStr, out int parsed))
                warningMB = parsed;

            if (sizeMB >= warningMB)
            {
                OnStorageWarning?.Invoke(sizeMB);
            }
        }
        catch
        {
            // 容量檢查失敗不影響主流程
        }
    }
}