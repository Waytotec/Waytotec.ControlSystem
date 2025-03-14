using System.Windows.Input;

namespace Waytotec.ControlSystem.App.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private string _title = "Waytotec Control System";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
