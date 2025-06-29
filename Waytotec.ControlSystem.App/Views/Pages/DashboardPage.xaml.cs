using System.Windows.Input;
using Waytotec.ControlSystem.App.ViewModels.Pages;
using Waytotec.ControlSystem.Core.Models;
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

        private void CameraGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (CameraGrid.SelectedItem is CameraInfo camera && camera != null)
            {
                RtspViewer.Load(ip: camera.Ip, stream: "stream0");
            }
        }

        private void RtspViewer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 현재 선택된 카메라의 IP 가져오기
            string ip = "192.168.1.120"; // 기본값
            string stream = "stream0";

            if (CameraGrid.SelectedItem is CameraInfo selectedCamera)
            {
                ip = selectedCamera.Ip;
            }

            var popup = new RtspPopupWindow(ip, stream);
            popup.Show();
        }
    }
}