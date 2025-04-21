using Waytotec.ControlSystem.App.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Waytotec.ControlSystem.App.Views
{
    public partial class UiWindow : INavigationWindow
    {
        private readonly SettingsService _settingsService;
        public UiWindowViewModel ViewModel { get; }

        public UiWindow(
            UiWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            SettingsService settingsService
            )
        {
            ViewModel = viewModel;
            _settingsService = settingsService;
            DataContext = this;

            InitializeComponent();
            InitTheme();

            SetPageService(navigationViewPageProvider);
            navigationService.SetNavigationControl(RootNavigation);
        }


        private void InitTheme()
        {
            SystemThemeWatcher.Watch(this);

            var theme = _settingsService.Settings.Theme == "Light"
                ? Wpf.Ui.Appearance.ApplicationTheme.Light
                : Wpf.Ui.Appearance.ApplicationTheme.Dark;

            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme);
        }

        #region INavigationWindow methods

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) => RootNavigation.SetPageProviderService(navigationViewPageProvider);

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        /// <summary>
        /// Raises the closed event.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }

        INavigationView INavigationWindow.GetNavigation()
        {
            throw new NotImplementedException();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}
