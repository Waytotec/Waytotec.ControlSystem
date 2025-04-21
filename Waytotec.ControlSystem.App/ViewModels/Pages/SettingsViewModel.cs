using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Waytotec.ControlSystem.App.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

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
            var theme = _settingsService.Settings.Theme == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark;
            ApplicationThemeManager.Apply(theme);

            CurrentTheme = theme;
            AppVersion = $"Device Control System - {GetAssemblyVersion()}";

            _isInitialized = true;
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            if (parameter == "theme_light")
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
                CurrentTheme = Wpf.Ui.Appearance.ApplicationTheme.Light;
                _settingsService.Settings.Theme = "Light";
            }
            else
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
                CurrentTheme = Wpf.Ui.Appearance.ApplicationTheme.Dark;
                _settingsService.Settings.Theme = "Dark";
            }

            _settingsService.Save();
        }
    }
}
