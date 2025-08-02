using System.Reflection;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Waytotec.ControlSystem.App.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private int _themeSelectedIndex = 1; // Dark 기본값

        [ObservableProperty]
        private int _backdropSelectedIndex = 1; // Mica 기본값

        partial void OnThemeSelectedIndexChanged(int value)
        {
            var theme = value == 0 ? "Light" : "Dark";
            _settingsService.Settings.Theme = theme;
            _settingsService.Save();
            ApplyTheme(theme);
        }
        partial void OnBackdropSelectedIndexChanged(int value)
        {
            var backdropTypes = new[] { "None", "Mica", "Acrylic", "Tabbed" };
            var backdropType = backdropTypes[value];
            _settingsService.Settings.WindowBackdropType = backdropType;
            _settingsService.Save();
            ApplyBackdrop(backdropType);
        }

        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            // 설정 값에서 인덱스 설정
            ThemeSelectedIndex = _settingsService.Settings.Theme == "Light" ? 0 : 1;

            var backdropTypes = new[] { "None", "Mica", "Acrylic", "Tabbed" };
            BackdropSelectedIndex = Array.IndexOf(backdropTypes, _settingsService.Settings.WindowBackdropType);
            if (BackdropSelectedIndex < 0) BackdropSelectedIndex = 1; // Mica 기본값

            _isInitialized = true;
        }


        private void ApplyTheme(string theme)
        {
            var appTheme = theme == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark;
            if (ApplicationThemeManager.GetAppTheme() == appTheme) return;
            ApplicationThemeManager.Apply(appTheme);
        }

        private void ApplyBackdrop(string backdropType)
        {
            var backdrop = backdropType switch
            {
                "Mica" => WindowBackdropType.Mica,
                "Acrylic" => WindowBackdropType.Acrylic,
                "Tabbed" => WindowBackdropType.Tabbed,
                _ => WindowBackdropType.None
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is Views.UiWindow mainWindow)
                {
                    mainWindow.ApplyBackdropType(backdropType);
                }
            });
        }

    }
}
