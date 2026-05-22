namespace StockRankTracker.Contracts.IServices;

public interface IFetchOrchestrator
{
    /// <summary>
    /// 執行一次抓取並存入資料庫
    /// 根據當前時段決定走盤中、收盤、補抓或不處理
    /// </summary>
    /// <param name="fetchSource">抓取來源："Scheduled" 或 "Manual"</param>
    Task FetchAndSaveAsync(string fetchSource = "Scheduled");

    /// <summary>最後一次抓取時間（供 UI 顯示）</summary>
    DateTime? LastFetchTime { get; }

    /// <summary>最後一次抓取是否成功</summary>
    bool LastFetchSuccess { get; }

    /// <summary>最後一次錯誤訊息</summary>
    string LastErrorMessage { get; }

    /// <summary>抓取完成事件（通知 ViewModel 更新 UI）</summary>
    event Action? OnFetchCompleted;

    /// <summary>資料庫容量超過警示門檻事件</summary>
    event Action<double>? OnStorageWarning;

    /// <summary>需要提示使用者的訊息（例如靜止期、資料已存在等）</summary>
    event Action<string>? OnUserMessage;
}
