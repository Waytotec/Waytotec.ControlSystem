using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Waytotec.ControlSystem.App.ViewModels.Pages;
using Waytotec.ControlSystem.Core.Models;
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
            DataContext = ViewModel;

            InitializeComponent();
        }

        /// <summary>
        /// 클립보드에 복사
        /// </summary>
        private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCameras.Count > 0)
            {
                if (ViewModel.CopySelectedToClipboardCommand.CanExecute(null))
                {
                    ViewModel.CopySelectedToClipboardCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (ViewModel.SelectedCamera != null)
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


        /// <summary>
        /// 체크박스 클릭 이벤트 - 다중 선택 처리
        /// </summary>
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is DiscoveredCamera camera)
            {
                if (checkBox.IsChecked == true)
                {
                    if (!ViewModel.SelectedCameras.Contains(camera))
                    {
                        ViewModel.SelectedCameras.Add(camera);
                    }
                }
                else
                {
                    ViewModel.SelectedCameras.Remove(camera);
                }
            }
        }

        /// <summary>
        /// DataGrid 행 로딩 시 행 번호 설정
        /// </summary>
        private void CameraDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // 행 번호를 1부터 시작하도록 설정
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        /// <summary>
        /// DataGrid 행 언로딩 시 정리
        /// </summary>
        private void CameraDataGrid_UnloadingRow(object sender, DataGridRowEventArgs e)
        {
            // 행 번호 정리 (필요시)
        }

        /// <summary>
        /// 선택된 행 변경 시 처리
        /// </summary>
        private void CameraDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectedItems와 ViewModel의 SelectedCameras 동기화
                if (sender is DataGrid dataGrid)
            {
                // 추가된 항목들
                foreach (DiscoveredCamera camera in e.AddedItems)
                {
                    if (!ViewModel.SelectedCameras.Contains(camera))
                    {
                        ViewModel.SelectedCameras.Add(camera);
                    }
                }

                // 제거된 항목들
                foreach (DiscoveredCamera camera in e.RemovedItems)
                {
                    ViewModel.SelectedCameras.Remove(camera);
                }

                // 상태 메시지 업데이트
                if (ViewModel.SelectedCameras.Count > 0)
                {
                    var firstCamera = ViewModel.SelectedCameras.First();
                    if (ViewModel.SelectedCameras.Count == 1)
                    {
                        ViewModel.StatusMessage = $"선택됨: {firstCamera.IpAddressString} ({firstCamera.StatusText})";
                    }
                    else
                    {
                        ViewModel.StatusMessage = $"{ViewModel.SelectedCameras.Count}대 카메라 선택됨";
                    }
                }
            }
        }

        /// <summary>
        /// DataGrid 더블클릭 이벤트 - 웹 인터페이스 열기
        /// </summary>
        private void CameraDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //if (ViewModel.SelectedCamera != null)
            //{
            //    OpenWebInterface_Click(sender, e);
            //}

            if (ViewModel.SelectedCamera != null)
            {
                RtspViewer.Load(ip: ViewModel.SelectedCamera.IpAddressString, stream: "stream0");
            }
        }

        /// <summary>
        /// DataGrid 키보드 이벤트 처리
        /// </summary>
        private void CameraDataGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null) return;

            if (ViewModel.SelectedCamera != null)
            {
                switch (e.Key)
                {
                    // Ctrl+A: 모두 선택
                    case System.Windows.Input.Key.A when e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control:
                        if (ViewModel.SelectAllCommand.CanExecute(null))
                        {
                            dataGrid.SelectAll();
                            // ViewModel 동기화
                            ViewModel.SelectedCameras.Clear();
                            foreach (DiscoveredCamera camera in dataGrid.SelectedItems)
                            {
                                ViewModel.SelectedCameras.Add(camera);
                            }

                            e.Handled = true;
                        }
                        break;

                    // Escape: 선택 해제
                    case System.Windows.Input.Key.Escape:
                        // DataGrid 선택 해제
                        dataGrid.UnselectAll();
                        dataGrid.SelectedItems.Clear();

                        // ViewModel 동기화
                        ViewModel.SelectedCameras.Clear();
                        ViewModel.SelectedCamera = null;

                        // 상태 메시지 업데이트
                        ViewModel.StatusMessage = "선택이 해제되었습니다.";

                        e.Handled = true;
                        break;

                    // Ctrl+C: 선택된 항목 복사
                    case System.Windows.Input.Key.C when e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control:
                        CopyToClipboard_Click(sender, e);
                        e.Handled = true;
                        break;

                    // F5: 선택된 카메라들 새로고침
                    case System.Windows.Input.Key.F5:
                        if (ViewModel.SelectedCameras.Count > 0 && ViewModel.RefreshSelectedCamerasCommand.CanExecute(null))
                        {
                            _ = ViewModel.RefreshSelectedCamerasCommand.ExecuteAsync(null);
                            e.Handled = true;
                        }
                        else if (ViewModel.SelectedCamera != null && ViewModel.RefreshSelectedCommand.CanExecute(null))
                        {
                            _ = ViewModel.RefreshSelectedCommand.ExecuteAsync(null);
                            e.Handled = true;
                        }
                        break;

                    // Delete: 선택된 카메라들을 목록에서 제거 (선택적 기능)
                    case System.Windows.Input.Key.Delete:
                        if (ViewModel.SelectedCameras.Count > 0)
                        {
                            var result = MessageBox.Show(
                                $"선택된 {ViewModel.SelectedCameras.Count}대 카메라를 목록에서 제거하시겠습니까?",
                                "카메라 제거 확인",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (result == MessageBoxResult.Yes)
                            {
                                RemoveSelectedCameras();
                                e.Handled = true;
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 선택된 카메라들을 목록에서 제거
        /// </summary>
        private void RemoveSelectedCameras()
        {
            var camerasToRemove = ViewModel.SelectedCameras.ToList();

            foreach (var camera in camerasToRemove)
            {
                ViewModel.Cameras.Remove(camera);
            }

            ViewModel.SelectedCameras.Clear();
            ViewModel.DiscoveredCount = ViewModel.Cameras.Count;
            ViewModel.StatusMessage = $"{camerasToRemove.Count}대 카메라가 목록에서 제거되었습니다.";
        }

        /// <summary>
        /// 비주얼 트리에서 지정된 타입의 자식 요소 찾기
        /// </summary>
        private static T? GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            T? child = null;
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < numVisuals; i++)
            {
                var visual = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = visual as T ?? GetVisualChild<T>(visual);
                if (child != null)
                {
                    break;
                }
            }

            return child;
        }

        /// <summary>
        /// 비주얼 트리에서 특정 타입의 부모 요소 찾기
        /// </summary>
        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindVisualParent<T>(parentObject);
        }

        private void RtspViewer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 현재 선택된 카메라의 IP 가져오기
            string ip = "192.168.1.120"; // 기본값
            string stream = "stream0";

            if (ViewModel.SelectedCamera != null)
            {
                ip = ViewModel.SelectedCamera.IpAddressString;
            }

            var popup = new RtspPopupWindow(ip, stream);
            popup.Show();
        }
    }
}
