using Microsoft.EntityFrameworkCore;
using StockRankTracker.Models.Entities;
using System.IO;

namespace StockRankTracker.Data;

/// <summary>
/// EF Core 資料庫上下文
/// </summary>
public class DatabaseContext : DbContext
{
    // 四張資料表
    public DbSet<DailyCloseEntity> DailyCloseSnapshots { get; set; }
    public DbSet<IntradaySessionEntity> IntradaySessions { get; set; }
    public DbSet<IntradaySnapshotEntity> IntradaySnapshots { get; set; }
    public DbSet<AppSettingEntity> AppSettings { get; set; }

    // SQLite 檔案路徑
    private readonly string _dbPath;

    public DatabaseContext()
    {
        // 資料庫檔案放在執行檔同層目錄
        _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StockRankTracker.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ===== DailyCloseSnapshot =====
        modelBuilder.Entity<DailyCloseEntity>(entity =>
        {
            entity.ToTable("DailyCloseSnapshot");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            // 建立索引：依日期查詢收盤資料
            entity.HasIndex(e => e.SnapshotDate);
            // 新增：防止同一天同一名次重複寫入
            entity.HasIndex(e => new { e.SnapshotDate, e.Rank }).IsUnique();
        });

        // ===== IntradaySession =====
        modelBuilder.Entity<IntradaySessionEntity>(entity =>
        {
            entity.ToTable("IntradaySession");
            entity.HasKey(e => e.SessionId);
            entity.Property(e => e.SessionId).ValueGeneratedOnAdd();

            // 建立索引：依交易日查詢
            entity.HasIndex(e => e.TradingDate);
        });

        // ===== IntradaySnapshot =====
        modelBuilder.Entity<IntradaySnapshotEntity>(entity =>
        {
            entity.ToTable("IntradaySnapshot");
            entity.HasKey(e => e.SnapshotId);
            entity.Property(e => e.SnapshotId).ValueGeneratedOnAdd();

            // 建立索引：依 SessionId 查詢
            entity.HasIndex(e => e.SessionId);

            // FK 關聯：刪除 Session 時，自動 Cascade 刪除 Snapshot
            entity.HasOne<IntradaySessionEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== AppSettings（用 Key-Value 方式存設定）=====
        modelBuilder.Entity<AppSettingEntity>(entity =>
        {
            entity.ToTable("AppSettings");
            entity.HasKey(e => e.Key);
        });
    }

    /// <summary>
    /// 取得資料庫檔案路徑（供計算檔案大小用）
    /// </summary>
    public string GetDatabasePath() => _dbPath;
}
