using Waytotec.ControlSystem.App.ViewModels.Windows;
using Waytotec.ControlSystem.App.Views.Pages;
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
        
        private bool _isUserClosedPane;

        private bool _isPaneOpenedOrClosedFromCode;

        public UiWindow(
            UiWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            ISnackbarService snackbarService,
            IContentDialogService contentDialogService,
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
            contentDialogService.SetDialogHost(RootContentDialog);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);
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
            // Application.Current.Shutdown();
        }

        INavigationView INavigationWindow.GetNavigation()
        {
            throw new NotImplementedException();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }

        private void FluentWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isUserClosedPane)
            {
                return;
            }

            _isPaneOpenedOrClosedFromCode = true;
            RootNavigation.SetCurrentValue(NavigationView.IsPaneOpenProperty, e.NewSize.Width > 1200);
            _isPaneOpenedOrClosedFromCode = false;
        }

        private void RootNavigation_PaneClosed(NavigationView sender, RoutedEventArgs args)
        {
            if(_isPaneOpenedOrClosedFromCode)
                return;

            _isUserClosedPane = true;
        }

        private void RootNavigation_PaneOpened(NavigationView sender, RoutedEventArgs args)
        {
            if (_isPaneOpenedOrClosedFromCode)
                return;

            _isUserClosedPane = false;
        }

        private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
        {
            if (sender is not Wpf.Ui.Controls.NavigationView navigationView)
            {
                return;
            }

            RootNavigation.SetCurrentValue(
                NavigationView.HeaderVisibilityProperty,
                navigationView.SelectedItem?.TargetPageType == typeof(SettingsPage) ||
                navigationView.SelectedItem?.TargetPageType == typeof(DashboardPage)
                    ? Visibility.Visible
                    : Visibility.Collapsed
            );
        }
    }
}
