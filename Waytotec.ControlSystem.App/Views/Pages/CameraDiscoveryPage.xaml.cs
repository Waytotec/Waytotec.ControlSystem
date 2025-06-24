using Waytotec.ControlSystem.App.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Waytotec.ControlSystem.App.Views.Pages
{
    /// <summary>
    /// CameraDiscoveryPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CameraDiscoveryPage : INavigableView<CameraDiscoveryViewModel>
    {
        public CameraDiscoveryViewModel ViewModel { get; }

        public CameraDiscoveryPage(CameraDiscoveryViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        /// <summary>
        /// 클립보드에 복사
        /// </summary>
        private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCamera != null)
            {
                var camera = ViewModel.SelectedCamera;
                var text = $"IP: {camera.IpAddressString}\n" +
                          $"MAC: {camera.FormattedMacAddress}\n" +
                          $"시리얼: {camera.SerialNumber}\n" +
                          $"버전: {camera.Version}\n" +
                          $"상태: {camera.StatusText}";

                Clipboard.SetText(text);

                // 간단한 알림 (실제로는 Snackbar 등을 사용)
                MessageBox.Show("클립보드에 복사되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
