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
        private readonly Random _random = new();

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
            GenerateTestDataCommand = new AsyncRelayCommand(GenerateTestDataAsync, () => !IsScanning);

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