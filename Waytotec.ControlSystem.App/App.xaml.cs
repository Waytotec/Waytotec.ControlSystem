using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Waytotec.ControlSystem.App.ViewModels;
using Waytotec.ControlSystem.App.Views;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Infrastructure.Services;
using Waytotec.ControlSystem.IoC;

namespace Waytotec.ControlSystem.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _serviceProvider = ContainerConfig.Configure(services =>
        {
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<IDeviceService, MockDeviceService>();
        });

        var splash = new SplashView();
        splash.Show();

        var mainViewModel = _serviceProvider.GetService<MainViewModel>();
        await mainViewModel!.LoadDevicesAsync();

        splash.Close();

        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };
        mainWindow.Show();

        // 다시 ShutdownMode를 기본으로
        Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
}

