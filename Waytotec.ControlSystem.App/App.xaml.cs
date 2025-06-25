using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows.Threading;
using Waytotec.ControlSystem.App.Services;
using Waytotec.ControlSystem.App.ViewModels.Pages;
using Waytotec.ControlSystem.App.ViewModels.Windows;
using Waytotec.ControlSystem.App.Views;
using Waytotec.ControlSystem.App.Views.Pages;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Infrastructure.Services;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace Waytotec.ControlSystem.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)); })
        .ConfigureServices((context, services) =>
        {
            services.AddNavigationViewPageProvider();
            services.AddHostedService<ApplicationHostService>();

            // Theme manipulation
            services.AddSingleton<IThemeService, ThemeService>();

            // TaskBar manipulation
            services.AddSingleton<ITaskBarService, TaskBarService>();

            // Service containing navigation, same as INavigationWindow... but without window
            services.AddSingleton<INavigationService, NavigationService>();

            services.AddSingleton<INavigationWindow, UiWindow>();

            services.AddSingleton<UiWindowViewModel>();
            services.AddSingleton<DashboardPage>();
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<SettingsPage>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<ManualPage>();
            services.AddSingleton<ManualViewModel>();

            // 새로운 카메라 검색 관련 등록
            services.AddSingleton<CameraDiscoveryPage>();
            services.AddSingleton<CameraDiscoveryViewModel>();

            // 카메라 검색 서비스 등록 (기존 ICameraService와 새로운 ICameraDiscoveryService 모두 구현)
            services.AddSingleton<CameraService>();
            //services.AddSingleton<ICameraService>(provider => provider.GetRequiredService<CameraService>());
            //services.AddSingleton<ICameraDiscoveryService>(provider => provider.GetRequiredService<CameraService>());

            services.AddSingleton<IDeviceService, MockDeviceService>();
            // 또는 별도로 등록하려면:
            services.AddSingleton<ICameraService, CameraService>();
            services.AddSingleton<ICameraDiscoveryService, CameraDiscoveryService>();



            // SettingsService 등록
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Waytotec");
            Directory.CreateDirectory(appData);
            var settingsPath = Path.Combine(appData, "settings.json");
            var settingsService = new SettingsService(settingsPath);
            services.AddSingleton(settingsService);
        }).Build();
    // private ServiceProvider? _serviceProvider;
    public static IServiceProvider Services
    {
        get { return _host.Services; }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var splash = new SplashView();
        splash.Show();

        var dashboard = Services.GetService<DashboardViewModel>();
        await dashboard!.LoadDevicesAsync();

        splash.Close();

        await _host.StartAsync();
        Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    //private async void OnStartup(object sender, StartupEventArgs e)
    //{
    //    await _host.StartAsync();
    //}

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private async void OnExit(object sender, ExitEventArgs e)
    {
        try
        {
            // 1. 모든 윈도우의 컨트롤들 정리
            await CleanupAllWindowsAsync();

            // 2. ViewModel 정리
            await CleanupViewModelsAsync();

            // 3. 호스트 중지 (타임아웃 적용)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _host.StopAsync(cts.Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"종료 중 오류: {ex.Message}");
        }
        finally
        {
            _host.Dispose();
        }
    }

    private async Task CleanupAllWindowsAsync()
    {
        try
        {
            foreach (Window window in Windows)
            {
                if (window is UiWindow uiWindow)
                {
                    // 각 페이지의 RtspVideoViewer 정리
                    var dashboardPage = Services.GetService<DashboardPage>();
                    if (dashboardPage?.RtspViewer != null)
                    {
                        await dashboardPage.RtspViewer.StopAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"윈도우 정리 중 오류: {ex.Message}");
        }
    }

    private async Task CleanupViewModelsAsync()
    {
        try
        {
            // CameraDiscoveryViewModel 정리
            var cameraDiscoveryVM = Services.GetService<CameraDiscoveryViewModel>();
            if (cameraDiscoveryVM != null)
            {
                await cameraDiscoveryVM.OnNavigatedFromAsync();
                cameraDiscoveryVM = null;
            }

            // DashboardViewModel 정리
            var dashboardVM = Services.GetService<DashboardViewModel>();
            if (dashboardVM != null)
            {
                await dashboardVM.OnNavigatedFromAsync();
                dashboardVM = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ViewModel 정리 중 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"처리되지 않은 예외: {e.Exception}");
        e.Handled = true;

        MessageBox.Show(
            $"예상치 못한 오류가 발생했습니다.\n\n{e.Exception.Message}",
            "애플리케이션 오류",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}

