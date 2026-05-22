using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StockRankTracker.Contracts.ICrawlers;
using StockRankTracker.Contracts.IRepositories;
using StockRankTracker.Contracts.IServices;
using StockRankTracker.Data;
using StockRankTracker.Data.Repositories;
using StockRankTracker.Helpers;
using StockRankTracker.Services;
using StockRankTracker.Services.Crawlers;
using StockRankTracker.ViewModels;

namespace StockRankTracker;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. 建立 DI 容器
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 2. 初始化資料庫
        var db = _serviceProvider.GetRequiredService<DatabaseContext>();
        await db.Database.EnsureCreatedAsync();

        // 3. 建立並顯示主視窗
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // 4. 啟動時檢查是否需要補收盤資料
        await StartupCheckAsync();

        // 5. 載入既有的盤中資料
        var intradayVm = _serviceProvider.GetRequiredService<IntradayViewModel>();
        await intradayVm.LoadLatestDataAsync();

        // 6. 啟動排程
        var scheduler = _serviceProvider.GetRequiredService<ISchedulerService>();
        scheduler.Start();
    }

    /// <summary>
    /// 註冊所有服務到 DI 容器
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // === 基礎設施 ===
        services.AddSingleton<DatabaseContext>();
        services.AddSingleton<HttpClient>(sp =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept-Language", "zh-TW,zh;q=0.9");
            return client;
        });

        // === Repository ===
        services.AddSingleton<IStockRepository, StockRepository>();

        // === Crawler ===
        services.AddSingleton<IRankCrawler, TurnoverCrawler>();

        // === Services ===
        services.AddSingleton<IFetchOrchestrator, FetchOrchestrator>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        services.AddSingleton<IDispatcherService, DispatcherService>();

        // === ViewModels ===
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<IntradayViewModel>();
        services.AddSingleton<AfterMarketViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // === Views ===
        services.AddSingleton<MainWindow>();
    }

    /// <summary>
    /// 啟動時檢查是否需要補收盤資料
    /// 13:35~00:00 啟動：檢查當天有沒有收盤資料，沒有就自動補抓
    /// 00:00~09:00 啟動：檢查前一交易日有沒有收盤資料，沒有就自動補抓
    /// 09:00 之後啟動：前一交易日缺資料只顯示提示，無法自動補
    /// </summary>
    private async Task StartupCheckAsync()
    {
        try
        {
            var now = DateTime.Now;
            var repository = _serviceProvider!.GetRequiredService<IStockRepository>();
            var scheduler = _serviceProvider!.GetRequiredService<ISchedulerService>();

            var holidays = DateTimeHelper.GetHolidays(now.Year);
            var time = now.TimeOfDay;

            if (DateTimeHelper.IsTradingDay(now, holidays) && time >= new TimeSpan(13, 35, 0))
            {
                // 交易日的 13:35 之後：優先檢查當天有沒有收盤資料
                if (!await repository.HasDailyCloseAsync(now.Date))
                {
                    await scheduler.ExecuteFetchAsync("AutoFill");
                    return;
                }
            }

            if (DateTimeHelper.IsTradingDay(now, holidays) && time >= new TimeSpan(9, 0, 0) && time < new TimeSpan(13, 35, 0))
            {
                // 交易日的 09:00~13:35：無法補抓，只提示
                var previousDate = DateTimeHelper.GetPreviousTradingDate(now.Date);
                if (!await repository.HasDailyCloseAsync(previousDate))
                {
                    var intradayVm = _serviceProvider!.GetRequiredService<IntradayViewModel>();
                    intradayVm.UserMessage = $"前一交易日（{previousDate:MM/dd}）缺少收盤資料，請至設定頁匯入 CSV 補齊";
                }
                return;
            }

            // 其他所有情況（假日、交易日09:00前）：檢查前一個交易日
            var prevDate = DateTimeHelper.GetPreviousTradingDate(now.Date);
            if (!await repository.HasDailyCloseAsync(prevDate))
            {
                await scheduler.ExecuteFetchAsync("AutoFill");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartupCheck 錯誤：{ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider != null)
        {
            var scheduler = _serviceProvider.GetRequiredService<ISchedulerService>();
            scheduler.Stop();

            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }
}