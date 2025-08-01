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
            new NavigationViewItemSeparator(),
            new NavigationViewItem()
            {
                Content = "홈",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            },
            new NavigationViewItem()
            {
                Content = "카메라 검색",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Camera28 },                
                TargetPageType = typeof(Views.Pages.CameraDiscoveryPage)
            },
            new NavigationViewItem()
            {
                Content = "LPR Util",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Iot24},
                TargetPageType = typeof(Views.Pages.ManualPage)
            },
            new NavigationViewItem()
            {
                Content = "메뉴얼",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Document24},
                TargetPageType = typeof(Views.Pages.ManualPage)
            },
            new NavigationViewItemSeparator(),
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItemSeparator(),
            new NavigationViewItem()
            {
                Content = "프로그램 설정",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage),                
            },
            new NavigationViewItem()
            {
                Content = "프로그램 정보",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Info24 },
                TargetPageType = typeof(Views.Pages.SettingsPage),
                Margin = new System.Windows.Thickness(0, 0, 0, 20)
            },
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" },
            new MenuItem { Header = "Close", Tag = "tray_close" },
        };
    }
}
