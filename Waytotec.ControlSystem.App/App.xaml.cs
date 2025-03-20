using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;
using Waytotec.ControlSystem.App.Services;
using Waytotec.ControlSystem.App.ViewModels;
using Waytotec.ControlSystem.App.ViewModels.Pages;
using Waytotec.ControlSystem.App.ViewModels.Windows;
using Waytotec.ControlSystem.App.Views;
using Waytotec.ControlSystem.App.Views.Pages;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Infrastructure.Services;
using Waytotec.ControlSystem.IoC;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace Waytotec.ControlSystem.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _serviceProvider = ContainerConfig.Configure(services =>
        {
            services.AddNavigationViewPageProvider();

            services.AddHostedService<ApplicationHostService>();

            // Theme manipulation
            services.AddSingleton<IThemeService, ThemeService>();

            // TaskBar manipulation
            services.AddSingleton<ITaskBarService, TaskBarService>();

            // Service containing navigation, same as INavigationWindow... but without window
            services.AddSingleton<INavigationService, NavigationService>();

            // Main window with navigation
            // services.AddSingleton<INavigationWindow, UiWindow>();

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<UiWindowViewModel>();
            services.AddSingleton<DashboardPage>();
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<SettingsPage>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<IDeviceService, MockDeviceService>();
        });

        var splash = new SplashView();
        splash.Show();

        var mainViewModel = _serviceProvider.GetService<MainViewModel>();
        await mainViewModel!.LoadDevicesAsync();

        splash.Close();

        //var mainWindow = new MainWindow
        //{
        //    DataContext = mainViewModel
        //};
        //mainWindow.Show();

        var uiWindowViewModel = _serviceProvider.GetService<UiWindowViewModel>();
        var mainWindow = new UiWindow
        {
            DataContext = uiWindowViewModel
        };
        mainWindow.Show();

        // 다시 ShutdownMode를 기본으로
        Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    private async void OnStartup(object sender, StartupEventArgs e)
    {

    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    }
}

