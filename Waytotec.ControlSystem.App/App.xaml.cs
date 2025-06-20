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
            services.AddSingleton<IDeviceService, MockDeviceService>();
            services.AddSingleton<ICameraService, CameraService>();

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
        // 다시 ShutdownMode를 기본으로
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
        await _host.StopAsync();

        _host.Dispose();
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    }
}

