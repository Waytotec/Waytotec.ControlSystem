
using System.Windows.Input;
using Waytotec.ControlSystem.App.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Waytotec.ControlSystem.App.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

            InitializeComponent();
            this.Unloaded += DashboardPage_Unloaded;
        }

        private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            RtspViewer.Stop();
        }

        private void DeviceGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DeviceGrid.SelectedItem is Core.Models.DeviceStatus device && device != null)
            {
                RtspViewer.Load(ip: device.IPString, stream: "stream0");
            }


            //if (DataContext is UiWindow vm && vm.SelectedDevice != null)
            //{
            //    var detailWindow = new DeviceDetailWindow(vm.SelectedDevice);
            //    // detailWindow.Owner = this;
            //    detailWindow.ShowDialog();
            //}
        }
    }
}
