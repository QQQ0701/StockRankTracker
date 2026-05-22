using Microsoft.EntityFrameworkCore;
using StockRankTracker.Contracts.IRepositories;
using StockRankTracker.Models.Entities;
using System.IO;

namespace StockRankTracker.Data.Repositories;

/// <summary>
/// 股票資料存取實作
/// 負責所有資料庫的讀寫操作，包含收盤快照、盤中快照、資料庫管理、系統設定
/// </summary>
public class StockRepository : IStockRepository
{
    private readonly DatabaseContext _db;

    public StockRepository(DatabaseContext db)
    {
        _db = db;
    }

    // ==================== 收盤快照 ====================

    /// <summary>
    /// 儲存收盤快照（整批寫入）
    /// 如果該交易日已有資料則跳過，確保每個交易日只存一次
    /// </summary>
    public async Task SaveDailyCloseAsync(List<DailyCloseEntity> entities)
    {
        if (entities.Count == 0) return;

        var date = entities[0].SnapshotDate.Date;
        var exists = await _db.DailyCloseSnapshots
            .AnyAsync(e => e.SnapshotDate == date);

        if (exists) return;

        _db.DailyCloseSnapshots.AddRange(entities);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// 取得指定日期的收盤快照（依名次排序）
    /// </summary>
    public async Task<List<DailyCloseEntity>> GetDailyCloseAsync(DateTime date)
    {
        return await _db.DailyCloseSnapshots
            .Where(e => e.SnapshotDate == date.Date)
            .OrderBy(e => e.Rank)
            .ToListAsync();
    }

    /// <summary>
    /// 取得最新一天的收盤快照（不限定日期，找最近有資料的那天）
    /// </summary>
    public async Task<List<DailyCloseEntity>> GetLatestDailyCloseAsync()
    {
        var latestDate = await _db.DailyCloseSnapshots
            .OrderByDescending(e => e.SnapshotDate)
            .Select(e => e.SnapshotDate)
            .FirstOrDefaultAsync();

        if (latestDate == default)
            return new List<DailyCloseEntity>();

        return await GetDailyCloseAsync(latestDate);
    }

    /// <summary>
    /// 取得指定日期「之前」最近一天的收盤快照
    /// 用於盤中/盤後比對基準（例如傳入今天，會找到昨天的收盤資料）
    /// </summary>
    public async Task<List<DailyCloseEntity>> GetPreviousDailyCloseAsync(DateTime beforeDate)
    {
        var previousDate = await _db.DailyCloseSnapshots
            .Where(e => e.SnapshotDate < beforeDate.Date)
            .OrderByDescending(e => e.SnapshotDate)
            .Select(e => e.SnapshotDate)
            .FirstOrDefaultAsync();

        if (previousDate == default)
            return new List<DailyCloseEntity>();

        return await GetDailyCloseAsync(previousDate);
    }

    /// <summary>
    /// 檢查指定日期是否已有收盤資料
    /// 用於抓取前判斷是否需要存入，避免重複
    /// </summary>
    public async Task<bool> HasDailyCloseAsync(DateTime date)
    {
        return await _db.DailyCloseSnapshots
            .AnyAsync(e => e.SnapshotDate == date.Date);
    }

    /// <summary>
    /// 取得所有有收盤資料的日期清單（最新的在前）
    /// 用於盤後頁的日期下拉選單
    /// </summary>
    public async Task<List<DateTime>> GetAvailableDailyCloseDatesAsync()
    {
        return await _db.DailyCloseSnapshots
            .Select(e => e.SnapshotDate)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();
    }

    /// <summary>
    /// 清除指定天數以前的收盤資料（預設保留30天）
    /// 由設定頁的「清除舊資料」按鈕觸發
    /// </summary>
    public async Task DeleteOldDailyCloseAsync(int keepDays = 30)
    {
        var cutoff = DateTime.Today.AddDays(-keepDays);
        var oldData = await _db.DailyCloseSnapshots
            .Where(e => e.SnapshotDate < cutoff)
            .ToListAsync();

        if (oldData.Any())
        {
            _db.DailyCloseSnapshots.RemoveRange(oldData);
            await _db.SaveChangesAsync();
        }
    }

    // ==================== 盤中批次 ====================

    /// <summary>
    /// 儲存一筆盤中抓取記錄（回傳自動產生的 SessionId）
    /// 每次抓取行為都會記錄，不管資料有沒有變化
    /// </summary>
    public async Task<int> SaveIntradaySessionAsync(IntradaySessionEntity session)
    {
        _db.IntradaySessions.Add(session);
        await _db.SaveChangesAsync();
        return session.SessionId;
    }

    /// <summary>
    /// 取得指定日期的所有盤中抓取記錄（依抓取時間排序）
    /// 用於盤後頁的時間下拉選單、盤中比對上一筆快照
    /// </summary>
    public async Task<List<IntradaySessionEntity>> GetIntradaySessionsAsync(DateTime date)
    {
        return await _db.IntradaySessions
            .Where(e => e.TradingDate == date.Date)
            .OrderBy(e => e.FetchTime)
            .ToListAsync();
    }

    /// <summary>
    /// 清除盤中舊資料（依 FetchSource 分段處理）
    /// 排程抓取：依 keepDays 清除（與收盤資料同步）
    /// 手動抓取：只保留最新一天，更早的全部清除
    /// Snapshot 透過 Cascade 自動隨 Session 刪除
    /// </summary>
    public async Task CleanIntradayDataAsync(int keepDays = 30)
    {
        var cutoff = DateTime.Today.AddDays(-keepDays);

        // 1. 排程抓取的：依 keepDays 清除
        var oldScheduled = await _db.IntradaySessions
            .Where(e => e.FetchSource == "Scheduled" && e.TradingDate < cutoff)
            .ToListAsync();

        // 2. 手動抓取的：只保留最新一天
        var manualDates = await _db.IntradaySessions
            .Where(e => e.FetchSource == "Manual")
            .Select(e => e.TradingDate.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

        var oldManualDates = manualDates.Skip(1).ToList();

        var oldManual = await _db.IntradaySessions
            .Where(e => e.FetchSource == "Manual" && oldManualDates.Contains(e.TradingDate.Date))
            .ToListAsync();

        // 合併刪除
        var toDelete = oldScheduled.Concat(oldManual).ToList();

        if (toDelete.Any())
        {
            _db.IntradaySessions.RemoveRange(toDelete);
            await _db.SaveChangesAsync();
        }
    }

    // ==================== 盤中快照 ====================

    /// <summary>
    /// 儲存盤中快照明細（整批寫入，關聯到指定的 SessionId）
    /// 只在資料有變化時才會呼叫，節省儲存空間
    /// </summary>
    public async Task SaveIntradaySnapshotAsync(List<IntradaySnapshotEntity> snapshots)
    {
        _db.IntradaySnapshots.AddRange(snapshots);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// 取得指定 Session 的盤中快照明細（依名次排序）
    /// 用於盤後頁選擇特定時間點時顯示該時刻的排名
    /// </summary>
    public async Task<List<IntradaySnapshotEntity>> GetIntradaySnapshotAsync(int sessionId)
    {
        return await _db.IntradaySnapshots
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.Rank)
            .ToListAsync();
    }

    // ==================== 資料庫管理 ====================

    /// <summary>
    /// 取得資料庫檔案大小（單位：MB）
    /// 用於設定頁顯示和容量警示判斷
    /// </summary>
    public async Task<double> GetDatabaseSizeAsync()
    {
        return await Task.Run(() =>
        {
            var path = _db.GetDatabasePath();
            if (!File.Exists(path))
                return 0.0;

            var fileInfo = new FileInfo(path);
            return fileInfo.Length / (1024.0 * 1024.0);
        });
    }

    // ==================== 設定 ====================

    /// <summary>
    /// 讀取系統設定值（找不到回傳 null）
    /// </summary>
    public async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _db.AppSettings.FindAsync(key);
        return setting?.Value;
    }

    /// <summary>
    /// 寫入或更新系統設定值（Key 不存在就新增，存在就更新）
    /// </summary>
    public async Task SetSettingAsync(string key, string value)
    {
        var setting = await _db.AppSettings.FindAsync(key);
        if (setting == null)
        {
            _db.AppSettings.Add(new AppSettingEntity { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        await _db.SaveChangesAsync();
    }
}