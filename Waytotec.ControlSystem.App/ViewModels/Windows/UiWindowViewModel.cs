using System.Collections.ObjectModel;
using Microsoft.Extensions.Localization;
using Wpf.Ui.Controls;

namespace Waytotec.ControlSystem.App.ViewModels.Windows
{
    public partial class UiWindowViewModel() : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "WTT Device Control System";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Home",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            },
            new NavigationViewItem()
            {
                Content = "카메라 검색",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Camera28 },                
                TargetPageType = typeof(Views.Pages.CameraDiscoveryPage)
            },
            new NavigationViewItemSeparator(),
            new NavigationViewItem()
            {
                Content = "Manual",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Document24},
                TargetPageType = typeof(Views.Pages.ManualPage)
            },
            //new NavigationViewItem()
            //{
            //    Content = "Data",
            //    Icon = new SymbolIcon { Symbol = SymbolRegular.DataHistogram24 },
            //    TargetPageType = typeof(Views.Pages.DataPage)
            //}
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItemSeparator(),
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage),
                Margin = new System.Windows.Thickness(0, 0, 0, 20)
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" },
            new MenuItem { Header = "Close", Tag = "tray_close" },
        };
    }
}
