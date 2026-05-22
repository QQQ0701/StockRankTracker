using StockRankTracker.Models.Entities;

namespace StockRankTracker.Contracts.IRepositories;

/// <summary>
/// 股票資料存取介面
/// </summary>
public interface IStockRepository
{
    // ===== 收盤快照 =====

    /// <summary>存入收盤快照（100筆）</summary>
    Task SaveDailyCloseAsync(List<DailyCloseEntity> entities);

    /// <summary>取得指定日期的收盤快照</summary>
    Task<List<DailyCloseEntity>> GetDailyCloseAsync(DateTime date);

    /// <summary>取得最近一個交易日的收盤快照（作為比對基準）</summary>
    Task<List<DailyCloseEntity>> GetLatestDailyCloseAsync();

    /// <summary>取得所有有收盤資料的日期清單</summary>
    Task<List<DateTime>> GetAvailableDailyCloseDatesAsync();

    /// <summary>刪除指定天數以前的收盤資料</summary>
    Task DeleteOldDailyCloseAsync(int keepDays = 30);

    // ===== 盤中批次 =====

    /// <summary>存入盤中批次記錄，回傳 SessionId</summary>
    Task<int> SaveIntradaySessionAsync(IntradaySessionEntity session);

    /// <summary>取得指定日期的所有盤中批次</summary>
    Task<List<IntradaySessionEntity>> GetIntradaySessionsAsync(DateTime date);

    // ===== 盤中快照 =====

    /// <summary>存入盤中快照資料（100筆，關聯到指定 SessionId）</summary>
    Task SaveIntradaySnapshotAsync(List<IntradaySnapshotEntity> snapshots);

    /// <summary>取得指定 SessionId 的盤中快照</summary>
    Task<List<IntradaySnapshotEntity>> GetIntradaySnapshotAsync(int sessionId);

    // ===== 資料庫管理 =====

    /// <summary>取得資料庫檔案大小（MB）</summary>
    Task<double> GetDatabaseSizeAsync();

    // ===== 設定 =====

    /// <summary>讀取設定值</summary>
    Task<string?> GetSettingAsync(string key);

    /// <summary>寫入設定值</summary>
    Task SetSettingAsync(string key, string value);
    /// <summary>
    /// 取得指定日期「之前」最近一天的收盤快照
    /// 用於盤中/盤後比對基準
    /// </summary>
    Task<List<DailyCloseEntity>> GetPreviousDailyCloseAsync(DateTime beforeDate);

    /// <summary>
    /// 檢查指定日期是否已有收盤資料
    /// 用於抓取前判斷是否需要存入，避免重複
    /// </summary>
    Task<bool> HasDailyCloseAsync(DateTime date);

    /// <summary>
    /// 清除盤中舊資料（排程依 keepDays，手動只留最新一天）
    /// </summary>
    Task CleanIntradayDataAsync(int keepDays = 30);
}
