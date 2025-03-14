using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Waytotec.ControlSystem.App.ViewModels;
using Waytotec.ControlSystem.App.Views;
using Waytotec.ControlSystem.IoC;

namespace Waytotec.ControlSystem.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _serviceProvider = ContainerConfig.Configure(services =>
        {
            services.AddSingleton<MainViewModel>();
        });

        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetService<MainViewModel>()
        };
        mainWindow.Show();
    }
}

