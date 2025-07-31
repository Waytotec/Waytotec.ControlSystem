// Waytotec.ControlSystem.App/ViewModels/Pages/CameraDiscoveryViewModel.cs
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        private readonly Random _random = new();

        // 다중 선택을 위한 추가 속성들
        [ObservableProperty]
        private ObservableCollection<DiscoveredCamera> _selectedCameras = new();
                

        [ObservableProperty]
        private bool _isAllSelected = false;

        // 다중 선택 관련 명령들
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand UnselectAllCommand { get; }
        public IRelayCommand ToggleSelectAllCommand { get; }
        public IRelayCommand InvertSelectionCommand { get; }
        public IAsyncRelayCommand RefreshSelectedCamerasCommand { get; }
        public IRelayCommand CopySelectedToClipboardCommand { get; }
        public IRelayCommand ExportSelectedCommand { get; }

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

        public IAsyncRelayCommand GenerateTestDataCommand { get; }


        public IRelayCommand PingShellCommand { get; }
        public IRelayCommand OpenWebConfigShellCommand { get; }


        // 컬렉션 뷰 (필터링 및 정렬용)
        public ICollectionView CamerasView { get; }

        /// <summary>
        /// 선택된 카메라 개수 속성
        /// </summary>
        public int SelectedCount => SelectedCameras?.Count ?? 0;

        /// <summary>
        /// 전체 선택 버튼 텍스트 (바인딩용)
        /// </summary>
        public string SelectAllButtonText => IsAllSelected ? "선택 해제" : "전체 선택";


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
            BindingOperations.EnableCollectionSynchronization(SelectedCameras, _camerasLock);
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
            GenerateTestDataCommand = new AsyncRelayCommand(GenerateTestDataAsync, () => !IsScanning);
            PingShellCommand = new RelayCommand(PingShellStart);
            OpenWebConfigShellCommand = new RelayCommand(OpenWebConfigShellStart);

            // 다중 선택 명령 초기화 추가
            SelectAllCommand = new RelayCommand(SelectAll, () => !IsScanning && Cameras.Count > 0);
            UnselectAllCommand = new RelayCommand(UnselectAll, () => SelectedCameras.Count > 0);
            ToggleSelectAllCommand = new RelayCommand(ToggleSelectAll, () => Cameras.Count > 0);
            InvertSelectionCommand = new RelayCommand(InvertSelection, () => !IsScanning && Cameras.Count > 0);
            RefreshSelectedCamerasCommand = new AsyncRelayCommand(RefreshSelectedCamerasAsync, () => SelectedCameras.Count > 0 && !IsScanning);
            CopySelectedToClipboardCommand = new RelayCommand(CopySelectedToClipboard, () => SelectedCameras.Count > 0);
            ExportSelectedCommand = new RelayCommand(ExportSelectedToFile, () => SelectedCameras.Count > 0);

            // 이벤트 구독
            _discoveryService.CameraDiscovered += OnCameraDiscovered;
            _discoveryService.DiscoveryProgress += OnDiscoveryProgress;
            _discoveryService.DiscoveryCompleted += OnDiscoveryCompleted;

            //// SelectedCameras 컬렉션 변경 이벤트 구독
            //SelectedCameras.CollectionChanged += (s, e) =>
            //{
            //    SelectedCount = SelectedCameras.Count;
            //    IsAllSelected = SelectedCameras.Count == Cameras.Count && Cameras.Count > 0;

            //    // 명령 실행 가능 상태 업데이트
            //    UnselectAllCommand.NotifyCanExecuteChanged();
            //    RefreshSelectedCamerasCommand.NotifyCanExecuteChanged();
            //    CopySelectedToClipboardCommand.NotifyCanExecuteChanged();
            //    ExportSelectedCommand.NotifyCanExecuteChanged();
            //};

            // 카메라 리스트 변경 시 전체 선택 상태 업데이트
            Cameras.CollectionChanged += OnCamerasCollectionChanged;
            SelectedCameras.CollectionChanged += OnSelectedCamerasChanged;
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

                // 기존 카메라 목록 클리어
                Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_camerasLock)
                    {
                        Cameras.Clear();
                        DiscoveredCount = 0;
                    }
                });

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
                SelectedCameras.Clear();
                SelectedCamera = null;
                DiscoveredCount = 0;
                IsAllSelected = false;
                StatusMessage = "목록이 지워졌습니다.";
            }
            OnPropertyChanged(nameof(SelectAllButtonText));
            OnPropertyChanged(nameof(SelectedCount));
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
                    // 시간 기반 진행률 계산 (10초 기준으로 100% 달성)
                    double timeProgress = Math.Min(e.ElapsedTime.TotalSeconds / 10.0 * 100, 100);

                    // 카메라 발견 수에 따른 추가 진행률 (발견될 때마다 조금씩 증가)
                    double discoveryProgress = Math.Min(e.DiscoveredCount * 2, 20); // 최대 20% 추가

                    // 전체 진행률 = 시간 진행률 + 발견 진행률
                    ScanProgress = Math.Min(timeProgress + discoveryProgress, 100);

                    // 95% 이상에서 멈춤 방지를 위한 로직
                    if (e.ElapsedTime.TotalSeconds >= 9.5 && ScanProgress < 100)
                    {
                        ScanProgress = 100; // 마지막 0.5초에서 강제로 100% 완료
                    }

                    Debug.WriteLine($"[CameraDiscoveryViewModel] 진행률: {ScanProgress:F1}%, 시간: {e.ElapsedTime.TotalSeconds:F1}초, 발견: {e.DiscoveredCount}대");
                }

                // 상태 메시지 업데이트
                if (e.DiscoveredCount > 0)
                {
                    StatusMessage = $"카메라 검색 중... ({e.DiscoveredCount}대 발견)";
                }
                else
                {
                    StatusMessage = "카메라 검색 중...";
                }

                // 발견된 카메라 수 업데이트
                DiscoveredCount = e.DiscoveredCount;
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
                GenerateTestDataCommand.NotifyCanExecuteChanged();
            }
            if (e.PropertyName == nameof(Cameras))
            {
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
        /// 모두 선택
        /// </summary>
        private void SelectAll()
        {
            try
            {
                lock (_camerasLock)
                {
                    SelectedCameras.Clear();
                    foreach (var camera in Cameras)
                    {
                        SelectedCameras.Add(camera);
                    }
                    IsAllSelected = true;
                }

                // PropertyChanged 알림 수동 발생
                OnPropertyChanged(nameof(SelectAllButtonText));
                OnPropertyChanged(nameof(SelectedCount));

                // 명령 상태 업데이트
                UpdateCommands();

                StatusMessage = $"전체 선택됨: {SelectedCount}개";
            }
            catch (Exception ex)
            {
                StatusMessage = $"전체 선택 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 전체 선택 오류: {ex}");
            }
        }

        /// <summary>
        /// 모두 선택 해제
        /// </summary>
        private void UnselectAll()
        {
            try
            {
                lock (_camerasLock)
                {
                    SelectedCameras.Clear();
                    IsAllSelected = false;
                }

                // PropertyChanged 알림 수동 발생
                OnPropertyChanged(nameof(SelectAllButtonText));
                OnPropertyChanged(nameof(SelectedCount));

                // 명령 상태 업데이트
                UpdateCommands();

                StatusMessage = "전체 선택 해제됨";
            }
            catch (Exception ex)
            {
                StatusMessage = $"전체 선택 해제 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 전체 선택 해제 오류: {ex}");
            }
        }

        /// <summary>
        /// 전체 선택/해제 토글
        /// </summary>
        private void ToggleSelectAll()
        {
            if (IsAllSelected)
            {
                UnselectAll();
            }
            else
            {
                SelectAll();
            }
        }

        /// <summary>
        /// 카메라 컬렉션 변경 이벤트 핸들러
        /// </summary>
        private void OnCamerasCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 카메라 목록이 변경되면 전체 선택 상태 확인
            UpdateSelectAllState();

            // 명령 상태 업데이트
            UpdateCommands();
        }

        /// <summary>
        /// 선택된 카메라 컬렉션 변경 이벤트 핸들러
        /// </summary>
        private void OnSelectedCamerasChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateSelectAllState();
            OnPropertyChanged(nameof(SelectedCount));
            UpdateCommands();
        }


        /// <summary>
        /// 전체 선택 상태 업데이트
        /// </summary>
        private void UpdateSelectAllState()
        {
            lock (_camerasLock)
            {
                var previousState = IsAllSelected;

                if (Cameras.Count == 0)
                {
                    IsAllSelected = false;
                }
                else
                {
                    IsAllSelected = SelectedCameras.Count == Cameras.Count &&
                                   Cameras.All(camera => SelectedCameras.Contains(camera));
                }

                // 상태가 변경되었으면 UI 업데이트
                if (previousState != IsAllSelected)
                {
                    OnPropertyChanged(nameof(SelectAllButtonText));
                }
            }
        }

        /// <summary>
        /// 명령들의 CanExecute 상태 업데이트
        /// </summary>
        private void UpdateCommands()
        {
            SelectAllCommand.NotifyCanExecuteChanged();
            UnselectAllCommand.NotifyCanExecuteChanged();
            ToggleSelectAllCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
        }

        // OnPropertyChanged 메서드 오버라이드 (IsAllSelected 변경 시 SelectAllButtonText 업데이트)
        partial void OnIsAllSelectedChanged(bool value)
        {
            OnPropertyChanged(nameof(SelectAllButtonText));
            UpdateCommands();
        }

        /// <summary>
        /// 개별 카메라 선택/해제
        /// </summary>
        private void ToggleCameraSelection(DiscoveredCamera camera)
        {
            if (camera == null) return;

            lock (_camerasLock)
            {
                if (SelectedCameras.Contains(camera))
                {
                    SelectedCameras.Remove(camera);
                }
                else
                {
                    SelectedCameras.Add(camera);
                }
            }
        }

        /// <summary>
        /// 선택 반전
        /// </summary>
        private void InvertSelection()
        {
            var camerasToAdd = Cameras.Except(SelectedCameras).ToList();
            var camerasToRemove = SelectedCameras.ToList();

            foreach (var camera in camerasToRemove)
            {
                SelectedCameras.Remove(camera);
            }

            foreach (var camera in camerasToAdd)
            {
                SelectedCameras.Add(camera);
            }
        }

        /// <summary>
        /// 선택된 카메라들 새로고침
        /// </summary>
        private async Task RefreshSelectedCamerasAsync()
        {
            if (SelectedCameras.Count == 0) return;

            try
            {
                IsScanning = true;
                StatusMessage = $"선택된 {SelectedCameras.Count}대 카메라 새로고침 중...";

                var refreshTasks = SelectedCameras.Select(async camera =>
                {
                    try
                    {
                        var refreshedCamera = await _discoveryService.VerifyCameraAsync(
                            camera.IpAddress, CancellationToken.None);

                        if (refreshedCamera != null)
                        {
                            camera.Status = refreshedCamera.Status;
                            camera.Version = refreshedCamera.Version;
                            camera.LastSeen = refreshedCamera.LastSeen;
                        }
                        else
                        {
                            camera.Status = CameraStatus.Offline;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CameraDiscovery] 카메라 새로고침 오류 ({camera.IpAddressString}): {ex.Message}");
                        camera.Status = CameraStatus.Error;
                    }
                });

                await Task.WhenAll(refreshTasks);
                StatusMessage = $"선택된 카메라 새로고침 완료";
            }
            catch (Exception ex)
            {
                StatusMessage = $"새로고침 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 다중 새로고침 오류: {ex}");
            }
            finally
            {
                IsScanning = false;
            }
        }


        /// <summary>
        /// 선택된 카메라들 클립보드에 복사
        /// </summary>
        private void CopySelectedToClipboard()
        {
            if (SelectedCameras.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"선택된 카메라 정보 ({SelectedCameras.Count}대)");
            sb.AppendLine(new string('=', 50));

            foreach (var camera in SelectedCameras)
            {
                sb.AppendLine($"IP: {camera.IpAddressString}");
                sb.AppendLine($"MAC: {camera.FormattedMacAddress}");
                sb.AppendLine($"시리얼: {camera.SerialNumber}");
                sb.AppendLine($"버전: {camera.Version}");
                sb.AppendLine($"상태: {camera.StatusText}");
                sb.AppendLine(new string('-', 30));
            }

            try
            {
                Clipboard.SetText(sb.ToString());
                StatusMessage = $"선택된 {SelectedCameras.Count}대 카메라 정보를 클립보드에 복사했습니다.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"클립보드 복사 오류: {ex.Message}";
            }
        }

        /// <summary>
        /// 선택된 카메라들만 파일로 내보내기
        /// </summary>
        private void ExportSelectedToFile()
        {
            if (SelectedCameras.Count == 0) return;

            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일 (*.csv)|*.csv|텍스트 파일 (*.txt)|*.txt",
                    DefaultExt = "csv",
                    FileName = $"SelectedCameras_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("IP주소,MAC주소,시리얼번호,버전,상태,HTTP포트,RTSP포트");

                    foreach (var camera in SelectedCameras)
                    {
                        sb.AppendLine($"{camera.IpAddressString},{camera.FormattedMacAddress}," +
                                     $"{camera.SerialNumber},{camera.Version},{camera.StatusText}," +
                                     $"{camera.HttpPort},{camera.RtspPort}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    StatusMessage = $"선택된 카메라 목록 저장 완료: {Path.GetFileName(saveFileDialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"파일 저장 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 선택된 카메라 파일 저장 오류: {ex}");
            }
        }

        /// <summary>
        /// PowerShell을 이용한 Ping Shell 시작
        /// </summary>
        private void PingShellStart()
        {
            if (SelectedCameras.Count == 0) return;

            try
            {
                foreach (var camera in SelectedCameras)
                {
                    string scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "Ping_Shell.ps1");

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoExit -ExecutionPolicy Bypass -File \"{scriptPath}\" -ip {camera.IpAddressString}",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal,
                    };

                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ping Test 실행 오류: {ex.Message}";
                Debug.WriteLine($"[PingShellStart] Ping Test 실행 오류: {ex}");
            }
        }

        /// <summary>
        /// 기본 웹브라우저를 이용한 카메라 웹 인터페이스 열기 
        /// </summary>
        private void OpenWebConfigShellStart()
        {
            if (SelectedCameras.Count == 0) return;

            try
            {
                foreach (var camera in SelectedCameras)
                {
                    var url = $"http://{camera.IpAddressString}:{camera.HttpPort}";

                    // 기본 브라우저로 웹 인터페이스 열기
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"웹 인터페이스 실행 오류: {ex.Message}";
                Debug.WriteLine($"[OpenWebConfigShellStart] 웹 인터페이스 실행 오류: {ex}");
            }
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

        #region 테스트 데이터 생성 기능

        /// <summary>
        /// 테스트 데이터 100건 생성
        /// </summary>
        private async Task GenerateTestDataAsync()
        {
            try
            {
                IsScanning = true;
                StatusMessage = "테스트 데이터 생성 중...";
                ShowProgress = true;
                ScanProgress = 0;

                // UI 반응성을 위해 비동기로 처리
                await Task.Run(async () =>
                {
                    const int totalItems = 100;

                    // 기존 데이터 정리
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        lock (_camerasLock)
                        {
                            Cameras.Clear();
                        }
                    });

                    var testCameras = new List<DiscoveredCamera>();

                    for (int i = 0; i < totalItems; i++)
                    {
                        var camera = GenerateRandomCamera(i + 1);
                        testCameras.Add(camera);

                        // 진행률 업데이트
                        var progress = ((double)(i + 1) / totalItems) * 100;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ScanProgress = progress;
                            StatusMessage = $"테스트 데이터 생성 중... ({i + 1}/{totalItems})";
                        });

                        // UI 갱신을 위한 작은 지연
                        if (i % 10 == 0)
                        {
                            await Task.Delay(50);
                        }
                    }

                    // 모든 데이터를 한 번에 추가
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        lock (_camerasLock)
                        {
                            foreach (var camera in testCameras)
                            {
                                Cameras.Add(camera);
                            }
                            DiscoveredCount = Cameras.Count;
                        }

                        CamerasView.Refresh();
                        StatusMessage = $"테스트 데이터 생성 완료 - {totalItems}대";
                    });
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"테스트 데이터 생성 오류: {ex.Message}";
                Debug.WriteLine($"[CameraDiscovery] 테스트 데이터 생성 오류: {ex}");
            }
            finally
            {
                IsScanning = false;
                ShowProgress = false;
                ScanProgress = 0;
            }
        }

        /// <summary>
        /// 랜덤 카메라 데이터 생성
        /// </summary>
        private DiscoveredCamera GenerateRandomCamera(int index)
        {
            // 다양한 IP 대역 생성
            var ipRanges = new[]
            {
                "192.168.1.", "192.168.0.", "192.168.100.", "10.0.0.",
                "10.1.1.", "172.16.1.", "172.20.1.", "192.168.10."
            };

            var selectedRange = ipRanges[_random.Next(ipRanges.Length)];
            var ipLastOctet = _random.Next(1, 255);
            var ipAddress = IPAddress.Parse($"{selectedRange}{ipLastOctet}");

            // 카메라 제조사별 MAC 주소 OUI (처음 3바이트)
            var macOuis = new[]
            {
                "00:12:34", // 가상 제조사 A
                "AA:BB:CC", // 가상 제조사 B  
                "11:22:33", // 가상 제조사 C
                "FF:EE:DD", // 가상 제조사 D
                "AB:CD:EF", // 가상 제조사 E
                "12:AB:34", // 가상 제조사 F
            };

            var selectedOui = macOuis[_random.Next(macOuis.Length)];
            var macAddress = $"{selectedOui}:{_random.Next(0, 256):X2}:{_random.Next(0, 256):X2}:{_random.Next(0, 256):X2}";

            // 시리얼 번호 생성 (제조사별 패턴)
            var serialPatterns = new[]
            {
                $"WTC{_random.Next(100000, 999999):D6}",
                $"CAM{_random.Next(10000, 99999):D5}",
                $"IP{_random.Next(1000000, 9999999):D7}",
                $"DVC{DateTime.Now.Year}{_random.Next(1000, 9999):D4}",
                $"NET{_random.Next(100, 999):D3}{_random.Next(100, 999):D3}",
            };

            var serialNumber = serialPatterns[_random.Next(serialPatterns.Length)];

            // 펌웨어 버전 생성
            var majorVersion = _random.Next(1, 5);
            var minorVersion = _random.Next(0, 10);
            var buildVersion = _random.Next(0, 100);
            var version = $"v{majorVersion}.{minorVersion}.{buildVersion}";

            // 상태 랜덤 생성 (대부분 Online으로)
            var statuses = new[]
            {
                CameraStatus.Online,     // 70% 확률
                CameraStatus.Online,
                CameraStatus.Online,
                CameraStatus.Online,
                CameraStatus.Online,
                CameraStatus.Online,
                CameraStatus.Online,
                CameraStatus.Offline,    // 20% 확률
                CameraStatus.Offline,
                CameraStatus.Error       // 10% 확률
            };

            var status = statuses[_random.Next(statuses.Length)];

            // 마지막 확인 시간 (최근 24시간 내)
            var lastSeen = DateTime.Now.AddMinutes(-_random.Next(0, 1440));

            return new DiscoveredCamera
            {
                Id = $"Camera_{index:D3}",
                IpAddress = ipAddress,
                IpAddressString = ipAddress.ToString(),
                MacAddress = macAddress,
                SerialNumber = serialNumber,
                Version = version,
                Status = status,
                LastSeen = lastSeen,

                // 네트워크 정보
                SubnetMask = IPAddress.Parse("255.255.255.0"),
                Gateway = IPAddress.Parse($"{selectedRange}1"),

                // 포트 정보 (랜덤 변형)
                HttpPort = 80 + _random.Next(0, 3) * 8080, // 80, 8080, 16160
                RtspPort = 554 + _random.Next(0, 2) * 4000, // 554, 4554
                HttpJpegPort = 8080 + _random.Next(0, 10) * 10, // 8080-8170
                PtzPort = _random.Next(0, 100) > 70 ? 1024 + _random.Next(0, 1000) : 0 // 30% 확률로 PTZ 지원
            };
        }
        #endregion
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

            // 컬렉션 이벤트 구독 해제
            if (Cameras != null)
            {
                Cameras.CollectionChanged -= OnCamerasCollectionChanged;
            }

            if (SelectedCameras != null)
            {
                SelectedCameras.CollectionChanged -= OnSelectedCamerasChanged;
            }
            _refreshTimer?.Dispose();

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