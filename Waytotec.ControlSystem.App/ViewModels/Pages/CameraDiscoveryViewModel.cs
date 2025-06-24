// Waytotec.ControlSystem.App/ViewModels/Pages/CameraDiscoveryViewModel.cs
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Data;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Core.Models;
using Wpf.Ui.Abstractions.Controls;

namespace Waytotec.ControlSystem.App.ViewModels.Pages
{
    public partial class CameraDiscoveryViewModel : ObservableObject, INavigationAware, IDisposable
    {
        private readonly ICameraDiscoveryService _discoveryService;
        private readonly object _camerasLock = new();
        private readonly Timer _refreshTimer;
        private bool _disposed = false;

        [ObservableProperty]
        private ObservableCollection<DiscoveredCamera> _cameras = new();

        [ObservableProperty]
        private DiscoveredCamera? _selectedCamera;

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private bool _isContinuousMode = false;


        [ObservableProperty]
        private int _discoveredCount = 0;

        [ObservableProperty]
        private string _elapsedTime = "00:00";

        [ObservableProperty]
        private string _statusMessage = "준비완료";

        [ObservableProperty]
        private string _networkRange = "192.168.1.1";

        [ObservableProperty]
        private int _networkMask = 24;

        [ObservableProperty]
        private string _selectedSortColumn = "IP주소";

        [ObservableProperty]
        private bool _isAscendingSort = true;

        // 필터링 속성들
        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private bool _showOnlineOnly = false;

        [ObservableProperty]
        private bool _showOfflineOnly = false;

        [ObservableProperty]
        private double _scanProgress = 0;

        [ObservableProperty]
        private bool _showProgress = false;

        // 명령들
        public IAsyncRelayCommand ScanCommand { get; }
        public IAsyncRelayCommand StartContinuousScanCommand { get; }
        public IAsyncRelayCommand StopScanCommand { get; }
        public IAsyncRelayCommand ScanRangeCommand { get; }
        public IAsyncRelayCommand RefreshSelectedCommand { get; }
        public IRelayCommand ClearListCommand { get; }
        public IRelayCommand SortCommand { get; }
        public IRelayCommand ExportCommand { get; }
        public IRelayCommand FilterCommand { get; }

        // 컬렉션 뷰 (필터링 및 정렬용)
        public ICollectionView CamerasView { get; }

        // 정렬 옵션
        public List<string> SortOptions { get; } = new()
        {
            "IP주소", "MAC주소", "시리얼번호", "버전", "상태", "마지막확인"
        };

        public CameraDiscoveryViewModel(ICameraDiscoveryService discoveryService)
        {
            _discoveryService = discoveryService;

            // 컬렉션 뷰 설정
            BindingOperations.EnableCollectionSynchronization(Cameras, _camerasLock);
            CamerasView = CollectionViewSource.GetDefaultView(Cameras);
            CamerasView.Filter = FilterCameras;
            CamerasView.SortDescriptions.Add(new SortDescription("IpAddressString", ListSortDirection.Ascending));

            // 명령 초기화
            ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
            StartContinuousScanCommand = new AsyncRelayCommand(StartContinuousScanAsync, () => !IsScanning);
            StopScanCommand = new AsyncRelayCommand(StopScanAsync, () => IsScanning);
            ScanRangeCommand = new AsyncRelayCommand(ScanRangeAsync, () => !IsScanning);
            RefreshSelectedCommand = new AsyncRelayCommand(RefreshSelectedAsync, () => SelectedCamera != null && !IsScanning);
            ClearListCommand = new RelayCommand(ClearList, () => !IsScanning);
            SortCommand = new RelayCommand<string>(SortBy);
            ExportCommand = new RelayCommand(ExportToFile, () => Cameras.Count > 0);
            FilterCommand = new RelayCommand(ApplyFilter);

            // 이벤트 구독
            _discoveryService.CameraDiscovered += OnCameraDiscovered;
            _discoveryService.DiscoveryProgress += OnDiscoveryProgress;
            _discoveryService.DiscoveryCompleted += OnDiscoveryCompleted;

            // 속성 변경 감지
            PropertyChanged += OnPropertyChanged;

            // 주기적 새로고침 타이머 (5초마다 LastSeen 업데이트)
            _refreshTimer = new Timer(RefreshLastSeenTimes, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public async Task OnNavigatedToAsync()
        {
            StatusMessage = "카메라 검색 준비완료";
            await Task.CompletedTask;
        }

        public async Task OnNavigatedFromAsync()
        {
            if (IsScanning)
            {
                await StopScanAsync();
            }
        }

        /// <summary>
        /// 단발성 검색
        /// </summary>
        private async Task ScanAsync()
        {
            try
            {
                IsScanning = true;
                IsContinuousMode = false;
                StatusMessage = "카메라 검색 중...";
                ShowProgress = true;
                ScanProgress = 0;

                var stopwatch = Stopwatch.StartNew();

                var cameras = await _discoveryService.DiscoverCamerasAsync(
                    TimeSpan.FromSeconds(10),
                    CancellationToken.None);

                stopwatch.Stop();

                StatusMessage = $"검색 완료 - {cameras.Count()}대 발견 ({stopwatch.Elapsed.TotalSeconds:F1}초)";
                DiscoveredCount = cameras.Count();
                ElapsedTime = $"{stopwatch.Elapsed.Minutes:D2}:{stopwatch.Elapsed.Seconds:D2}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"검색 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 검색 오류: {ex}");
            }
            finally
            {
                IsScanning = false;
                ShowProgress = false;
                ScanProgress = 0;
            }
        }

        /// <summary>
        /// 지속적 검색 시작
        /// </summary>
        private async Task StartContinuousScanAsync()
        {
            try
            {
                IsScanning = true;
                IsContinuousMode = true;
                StatusMessage = "실시간 검색 중...";
                ShowProgress = false;

                await _discoveryService.StartContinuousDiscoveryAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                StatusMessage = $"실시간 검색 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 실시간 검색 오류: {ex}");
            }
        }

        /// <summary>
        /// 검색 중지
        /// </summary>
        private async Task StopScanAsync()
        {
            try
            {
                await _discoveryService.StopDiscoveryAsync();
                IsScanning = false;
                IsContinuousMode = false;
                StatusMessage = "검색 중지됨";
                ShowProgress = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"중지 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 중지 오류: {ex}");
            }
        }

        /// <summary>
        /// IP 대역 검색
        /// </summary>
        private async Task ScanRangeAsync()
        {
            try
            {
                if (!IPAddress.TryParse(NetworkRange, out var baseIp))
                {
                    StatusMessage = "잘못된 IP 주소 형식입니다.";
                    return;
                }

                IsScanning = true;
                StatusMessage = $"대역 검색 중: {NetworkRange}/{NetworkMask}";
                ShowProgress = true;
                ScanProgress = 0;

                var (startIp, endIp) = CalculateIpRange(baseIp, NetworkMask);

                var stopwatch = Stopwatch.StartNew();
                var cameras = await _discoveryService.DiscoverCamerasInRangeAsync(
                    startIp, endIp, CancellationToken.None);
                stopwatch.Stop();

                StatusMessage = $"대역 검색 완료 - {cameras.Count()}대 발견 ({stopwatch.Elapsed.TotalSeconds:F1}초)";
                DiscoveredCount = cameras.Count();
            }
            catch (Exception ex)
            {
                StatusMessage = $"대역 검색 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 대역 검색 오류: {ex}");
            }
            finally
            {
                IsScanning = false;
                ShowProgress = false;
                ScanProgress = 0;
            }
        }

        /// <summary>
        /// 선택된 카메라 새로고침
        /// </summary>
        private async Task RefreshSelectedAsync()
        {
            if (SelectedCamera == null) return;

            try
            {
                StatusMessage = $"{SelectedCamera.IpAddressString} 새로고침 중...";

                var camera = await _discoveryService.VerifyCameraAsync(
                    SelectedCamera.IpAddress, CancellationToken.None);

                if (camera != null)
                {
                    // 선택된 카메라 정보 업데이트
                    SelectedCamera.Status = camera.Status;
                    SelectedCamera.Version = camera.Version;
                    SelectedCamera.LastSeen = camera.LastSeen;

                    StatusMessage = $"{SelectedCamera.IpAddressString} 새로고침 완료";
                }
                else
                {
                    SelectedCamera.Status = CameraStatus.Offline;
                    StatusMessage = $"{SelectedCamera.IpAddressString} 응답 없음";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"새로고침 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 새로고침 오류: {ex}");
            }
        }

        /// <summary>
        /// 목록 지우기
        /// </summary>
        private void ClearList()
        {
            lock (_camerasLock)
            {
                Cameras.Clear();
                SelectedCamera = null;
                DiscoveredCount = 0;
                StatusMessage = "목록이 지워졌습니다.";
            }
        }

        /// <summary>
        /// 정렬
        /// </summary>
        private void SortBy(string? columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return;

            var direction = (SelectedSortColumn == columnName && IsAscendingSort)
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            CamerasView.SortDescriptions.Clear();

            var propertyName = columnName switch
            {
                "IP주소" => "IpAddressString",
                "MAC주소" => "FormattedMacAddress",
                "시리얼번호" => "SerialNumber",
                "버전" => "Version",
                "상태" => "Status",
                "마지막확인" => "LastSeen",
                _ => "IpAddressString"
            };

            CamerasView.SortDescriptions.Add(new SortDescription(propertyName, direction));

            SelectedSortColumn = columnName;
            IsAscendingSort = direction == ListSortDirection.Ascending;
        }

        /// <summary>
        /// 파일로 내보내기
        /// </summary>
        private void ExportToFile()
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일 (*.csv)|*.csv|텍스트 파일 (*.txt)|*.txt",
                    DefaultExt = "csv",
                    FileName = $"CameraList_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("ID,IP주소,MAC주소,시리얼번호,버전,상태,마지막확인,HTTP포트,RTSP포트");

                    foreach (var camera in Cameras)
                    {
                        sb.AppendLine($"{camera.Id},{camera.IpAddressString},{camera.FormattedMacAddress}," +
                                     $"{camera.SerialNumber},{camera.Version},{camera.StatusText}," +
                                     $"{camera.LastSeen:yyyy-MM-dd HH:mm:ss},{camera.HttpPort},{camera.RtspPort}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    StatusMessage = $"파일 저장 완료: {Path.GetFileName(saveFileDialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"파일 저장 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 파일 저장 오류: {ex}");
            }
        }

        /// <summary>
        /// 필터 적용
        /// </summary>
        private void ApplyFilter()
        {
            CamerasView.Refresh();
        }

        /// <summary>
        /// 카메라 필터링
        /// </summary>
        private bool FilterCameras(object item)
        {
            if (item is not DiscoveredCamera camera) return false;

            // 텍스트 필터
            if (!string.IsNullOrEmpty(FilterText))
            {
                var searchText = FilterText.ToLower();
                if (!camera.IpAddressString.ToLower().Contains(searchText) &&
                    !camera.MacAddress.ToLower().Contains(searchText) &&
                    !camera.SerialNumber.ToLower().Contains(searchText) &&
                    !camera.Version.ToLower().Contains(searchText))
                {
                    return false;
                }
            }

            // 상태 필터
            if (ShowOnlineOnly && camera.Status != CameraStatus.Online)
                return false;

            if (ShowOfflineOnly && camera.Status == CameraStatus.Online)
                return false;

            return true;
        }

        /// <summary>
        /// 이벤트 핸들러들
        /// </summary>
        private void OnCameraDiscovered(object? sender, CameraDiscoveredEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_camerasLock)
                {
                    var existing = Cameras.FirstOrDefault(c => c.IpAddress.Equals(e.Camera.IpAddress));
                    if (existing != null)
                    {
                        // 기존 카메라 정보 업데이트
                        existing.Status = e.Camera.Status;
                        existing.LastSeen = e.Camera.LastSeen;
                        existing.Version = e.Camera.Version;
                    }
                    else
                    {
                        Cameras.Add(e.Camera);
                    }

                    DiscoveredCount = Cameras.Count;
                }
            });
        }

        private void OnDiscoveryProgress(object? sender, DiscoveryProgressEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ElapsedTime = $"{e.ElapsedTime.Minutes:D2}:{e.ElapsedTime.Seconds:D2}";
                if (ShowProgress)
                {
                    ScanProgress = Math.Min(e.ElapsedTime.TotalSeconds / 10.0 * 100, 100);
                }
            });
        }

        private void OnDiscoveryCompleted(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!IsContinuousMode)
                {
                    IsScanning = false;
                    ShowProgress = false;
                    StatusMessage = $"검색 완료 - {DiscoveredCount}대 발견";
                }
            });
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 필터 관련 속성이 변경되면 자동으로 필터 적용
            if (e.PropertyName == nameof(FilterText) ||
                e.PropertyName == nameof(ShowOnlineOnly) ||
                e.PropertyName == nameof(ShowOfflineOnly))
            {
                ApplyFilter();
            }

            // 명령 실행 가능 상태 업데이트
            if (e.PropertyName == nameof(IsScanning) || e.PropertyName == nameof(SelectedCamera))
            {
                ScanCommand.NotifyCanExecuteChanged();
                StartContinuousScanCommand.NotifyCanExecuteChanged();
                StopScanCommand.NotifyCanExecuteChanged();
                ScanRangeCommand.NotifyCanExecuteChanged();
                RefreshSelectedCommand.NotifyCanExecuteChanged();
                ClearListCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// IP 대역 계산
        /// </summary>
        private (IPAddress startIp, IPAddress endIp) CalculateIpRange(IPAddress baseIp, int maskBits)
        {
            var ipBytes = baseIp.GetAddressBytes();
            var ip = BitConverter.ToUInt32(ipBytes.Reverse().ToArray(), 0);

            var mask = ~((uint)(Math.Pow(2, 32 - maskBits) - 1));
            var networkIp = ip & mask;
            var broadcastIp = networkIp | ~mask;

            var startBytes = BitConverter.GetBytes(networkIp + 1).Reverse().ToArray();
            var endBytes = BitConverter.GetBytes(broadcastIp - 1).Reverse().ToArray();

            return (new IPAddress(startBytes), new IPAddress(endBytes));
        }

        /// <summary>
        /// 주기적 LastSeen 시간 업데이트
        /// </summary>
        private void RefreshLastSeenTimes(object? state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // LastSeenText 속성 업데이트를 위해 PropertyChanged 발생
                foreach (var camera in Cameras)
                {
                    camera.OnPropertyChanged(nameof(camera.LastSeenText));
                }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;

            _refreshTimer?.Dispose();

            if (_discoveryService != null)
            {
                _discoveryService.CameraDiscovered -= OnCameraDiscovered;
                _discoveryService.DiscoveryProgress -= OnDiscoveryProgress;
                _discoveryService.DiscoveryCompleted -= OnDiscoveryCompleted;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    if (IsScanning)
                    {
                        await StopScanAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CameraDiscovery] Dispose 중 오류: {ex}");
                }
            });

            _disposed = true;
        }
    }
}