namespace StockRankTracker.Models.Entities;

/// <summary>
/// 設定表（Key-Value 存放系統設定）
/// </summary>
public class AppSettingEntity
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
