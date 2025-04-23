
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

        private void RtspViewer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // RtspVideoViewer me = sender as RtspVideoViewer;

            string ip = "192.168.1.120"; // 현재 재생 중인 IP를 여기서 가져오세요
            string stream = "stream0";   // 현재 stream 값도 같이 넘겨야 합니다

            var popup = new RtspPopupWindow(ip, stream);
            popup.Show();
        }
    }
}
