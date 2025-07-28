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

        private void CameraDataGrid_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
         {
            // 불필요한 커스텀 선택 로직 제거
            // DataGrid의 기본 선택 동작을 그대로 사용
            // 특별한 동작이 필요 없다면 이 메서드는 비워두거나 삭제해도 됩니다.
            //var dataGrid = sender as DataGrid;
            //if (dataGrid == null) return;

            //// 클릭 위치에서 DataGridRow 찾기
            //DependencyObject dep = (DependencyObject)e.OriginalSource;
            //while (dep != null && !(dep is DataGridRow))
            //    dep = VisualTreeHelper.GetParent(dep);

            //if (dep is DataGridRow row)
            //{
            //    // 행이 선택되지 않았다면 선택
            //    if (!row.IsSelected)
            //    {
            //        row.IsSelected = true;
            //        dataGrid.SelectedItem = row.Item;
            //    }
            //}
        }


        /// <summary>
        /// 마우스 위치에서 카메라 찾기 (개선된 로직)
        /// </summary>
        private DiscoveredCamera? FindCameraFromMousePosition(DataGrid dataGrid, MouseButtonEventArgs e)
        {
            // 방법 1: 직접적인 DataGridRow 찾기
            var hit = e.OriginalSource as DependencyObject;
            while (hit != null && !(hit is DataGridRow) && !(hit is System.Windows.Documents.Run))
            {
                hit = VisualTreeHelper.GetParent(hit);
            }

            if (hit is DataGridRow row && row.DataContext is DiscoveredCamera directCamera)
            {
                return directCamera;
            }

            // 방법 2: 마우스 위치 기반으로 찾기 (Row/Column 사이 클릭 처리)
            Point mousePosition = e.GetPosition(dataGrid);

            // HitTest를 이용한 방법
            var hitTest = VisualTreeHelper.HitTest(dataGrid, mousePosition);
            if (hitTest?.VisualHit != null)
            {
                var dataGridRow = FindVisualParent<DataGridRow>(hitTest.VisualHit);
                if (dataGridRow?.DataContext is DiscoveredCamera hitTestCamera)
                {
                    return hitTestCamera;
                }
            }

            // 방법 3: 계산을 통한 행 찾기 (최후의 방법)
            var scrollViewer = GetVisualChild<ScrollViewer>(dataGrid);
            if (scrollViewer != null)
            {
                // 헤더 높이를 고려한 실제 콘텐츠 영역에서의 Y 위치
                double contentY = mousePosition.Y - dataGrid.ColumnHeaderHeight;
                if (contentY > 0 && dataGrid.RowHeight > 0)
                {
                    // 클릭한 위치에 해당하는 Row 인덱스 계산
                    int rowIndex = (int)(contentY / dataGrid.RowHeight);

                    // 스크롤 위치도 고려
                    rowIndex += (int)(scrollViewer.VerticalOffset / dataGrid.RowHeight);

                    // 유효한 인덱스인지 확인
                    if (rowIndex >= 0 && rowIndex < dataGrid.Items.Count)
                    {
                        if (dataGrid.Items[rowIndex] is DiscoveredCamera calculatedCamera)
                        {
                            return calculatedCamera;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Shift 선택 처리 (범위 선택)
        /// </summary>
        private void HandleShiftSelection(DataGrid dataGrid, DiscoveredCamera targetCamera)
        {
            if (dataGrid.SelectedItems.Count == 0) return;

            var cameras = ViewModel.Cameras.ToList();
            var lastSelectedCamera = ViewModel.SelectedCameras.LastOrDefault();

            if (lastSelectedCamera == null) return;

            var startIndex = cameras.IndexOf(lastSelectedCamera);
            var endIndex = cameras.IndexOf(targetCamera);

            if (startIndex >= 0 && endIndex >= 0)
            {
                var minIndex = Math.Min(startIndex, endIndex);
                var maxIndex = Math.Max(startIndex, endIndex);

                // 범위 내의 모든 카메라 선택
                for (int i = minIndex; i <= maxIndex; i++)
                {
                    var camera = cameras[i];
                    if (!dataGrid.SelectedItems.Contains(camera))
                    {
                        dataGrid.SelectedItems.Add(camera);
                    }
                    if (!ViewModel.SelectedCameras.Contains(camera))
                    {
                        ViewModel.SelectedCameras.Add(camera);
                    }
                }
            }
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
    }
}
