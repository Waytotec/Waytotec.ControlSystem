using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
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


        // VisualTree에서 특정 타입의 자식 요소를 찾는 헬퍼 메서드
        private static T GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;

                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }

                if (child != null)
                {
                    break;
                }
            }

            return child;
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
            // 선택된 카메라 정보 업데이트 (ViewModel에서 처리되지만 추가 로직 필요시 사용)
            if (ViewModel.SelectedCamera != null)
            {
                // 선택된 카메라에 대한 추가 처리
                Debug.WriteLine($"선택된 카메라: {ViewModel.SelectedCamera.IpAddressString}");
            }
        }

        private void CameraDataGrid_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 클릭한 위치에서 DataGridRow를 찾습니다
            var hit = e.OriginalSource as DependencyObject;

            while (hit != null && !(hit is DataGridRow) && !(hit is System.Windows.Documents.Run))
            {
                hit = VisualTreeHelper.GetParent(hit);
            }

            if (hit is DataGridRow row)
            {
                // Row를 찾았으면 선택합니다
                CameraDataGrid.SelectedItem = row.Item;
                row.IsSelected = true;
            }
            else
            {
                // Row를 찾지 못했지만 DataGrid 내부를 클릭한 경우
                // 마우스 위치에서 가장 가까운 Row를 찾습니다
                Point mousePosition = e.GetPosition(CameraDataGrid);

                // ScrollViewer를 찾습니다
                var scrollViewer = GetVisualChild<ScrollViewer>(CameraDataGrid);
                if (scrollViewer != null)
                {
                    // 헤더 높이를 고려한 실제 콘텐츠 영역에서의 Y 위치
                    double contentY = mousePosition.Y - CameraDataGrid.ColumnHeaderHeight;

                    if (contentY > 0)
                    {
                        // 클릭한 위치에 해당하는 Row 인덱스 계산
                        int rowIndex = (int)(contentY / CameraDataGrid.RowHeight);

                        // 스크롤 위치도 고려
                        if (scrollViewer != null)
                        {
                            rowIndex += (int)(scrollViewer.VerticalOffset / CameraDataGrid.RowHeight);
                        }

                        // 유효한 인덱스인지 확인하고 선택
                        if (rowIndex >= 0 && rowIndex < CameraDataGrid.Items.Count)
                        {
                            CameraDataGrid.SelectedIndex = rowIndex;
                        }
                    }
                }
            }
        }
    }
}
