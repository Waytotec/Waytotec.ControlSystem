using System.Windows.Input;
using Waytotec.ControlSystem.App.ViewModels;

namespace Waytotec.ControlSystem.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void DeviceGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.SelectedDevice != null)
            {
                var detailWindow = new DeviceDetailWindow(vm.SelectedDevice);
                detailWindow.Owner = this;
                detailWindow.ShowDialog();
            }
        }

    }
}

