using System.Diagnostics;
using Waytotec.ControlSystem.App.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Waytotec.ControlSystem.App.Views.Pages
{
    /// <summary>
    /// CameraDiscoveryPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CameraDiscoveryPageBackup : INavigableView<CameraDiscoveryViewModel>
    {
        public CameraDiscoveryViewModel ViewModel { get; }

        public CameraDiscoveryPageBackup(CameraDiscoveryViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

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

        /// <summary>
        /// 알림 메시지 표시 (간단한 구현)
        /// </summary>
        private void ShowNotification(string message)
        {
            // 실제 프로덕션에서는 Snackbar나 Toast 알림을 사용하는 것이 좋습니다
            MessageBox.Show(message, "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// DataGrid 더블클릭 이벤트 - 웹 인터페이스 열기
        /// </summary>
        private void CameraDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel.SelectedCamera != null)
            {
                OpenWebInterface_Click(sender, e);
            }
        }

        /// <summary>
        /// DataGrid 키보드 이벤트 처리
        /// </summary>
        private void CameraDataGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel.SelectedCamera != null)
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.C when e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control:
                        CopyToClipboard_Click(sender, e);
                        e.Handled = true;
                        break;

                    case System.Windows.Input.Key.F5:
                        if (ViewModel.RefreshSelectedCommand.CanExecute(null))
                        {
                            ViewModel.RefreshSelectedCommand.Execute(null);
                        }
                        e.Handled = true;
                        break;

                    case System.Windows.Input.Key.Enter:
                        OpenWebInterface_Click(sender, e);
                        e.Handled = true;
                        break;
                }
            }
        }

        /// <summary>
        /// 웹 인터페이스 열기
        /// </summary>
        private void OpenWebInterface_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCamera != null)
            {
                try
                {
                    var camera = ViewModel.SelectedCamera;
                    var url = $"http://{camera.IpAddressString}:{camera.HttpPort}";

                    // 기본 브라우저로 웹 인터페이스 열기
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);

                    // ShowNotification($"웹 인터페이스 열기: {url}");
                }
                catch (Exception ex)
                {
                    ShowNotification($"웹 인터페이스 열기 실패: {ex.Message}");
                }
            }
        }
    }
}
