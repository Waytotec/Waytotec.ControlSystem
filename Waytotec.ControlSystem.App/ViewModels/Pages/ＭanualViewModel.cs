using Wpf.Ui.Abstractions.Controls;

namespace Waytotec.ControlSystem.App.ViewModels.Pages
{
    public partial class ManualViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            _isInitialized = true;
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }
    }
}
