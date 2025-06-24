using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Core.Models;
using Wpf.Ui.Abstractions.Controls;

namespace Waytotec.ControlSystem.App.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private int _pingOffsetIndex = 0;
        private const int _maxWindowCount = 9;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [ObservableProperty]
        private DeviceStatus _selectedDevice;

        public ObservableCollection<DeviceStatus> Devices { get; } = new();
        private readonly IDeviceService _deviceService;
        private readonly ICameraService _cameraService;
        private readonly ICameraDiscoveryService _cameraDiscoveryService;


        public ObservableCollection<CameraInfo> Cameras { get; } = new();
        public ObservableCollection<DiscoveredCamera> DiscoveredCameras { get; } = new();

        [ObservableProperty]
        private bool _isCameraScanning = false;

        [ObservableProperty]
        private int _discoveredCameraCount = 0;

        [ObservableProperty]
        private string _cameraSearchStatus = "준비완료";

        public IAsyncRelayCommand ScanCommand { get; }
        public IAsyncRelayCommand QuickScanCommand { get; }

        private string _title = "Waytotec Device Control System";

        public DashboardViewModel(IDeviceService deviceService, ICameraService cameraService, ICameraDiscoveryService cameraDiscoveryService)
        {
            _deviceService = deviceService;
            _cameraService = cameraService;
            _cameraDiscoveryService = cameraDiscoveryService;

            ScanCommand = new AsyncRelayCommand(ScanAsync);
            QuickScanCommand = new AsyncRelayCommand(QuickScanAsync);

            // 카메라 검색 이벤트 구독
            _cameraDiscoveryService.CameraDiscovered += OnCameraDiscovered;
            _cameraDiscoveryService.DiscoveryProgress += OnDiscoveryProgress;
            _cameraDiscoveryService.DiscoveryCompleted += OnDiscoveryCompleted;
        }


        private async Task ScanAsync()
        {
            Cameras.Clear();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await foreach (var cam in _cameraService.FindCamerasAsync(cts.Token))
            {
                if (!Cameras.Any(c => c.Ip == cam.Ip))
                    Cameras.Add(cam);
            }
        }

        /// <summary>
        /// 새로운 빠른 검색 (새로운 검색 서비스 사용)
        /// </summary>
        private async Task QuickScanAsync()
        {
            try
            {
                IsCameraScanning = true;
                CameraSearchStatus = "카메라 검색 중...";
                DiscoveredCameras.Clear();

                var stopwatch = Stopwatch.StartNew();

                var cameras = await _cameraDiscoveryService.DiscoverCamerasAsync(
                    TimeSpan.FromSeconds(5), // 빠른 검색이므로 5초로 단축
                    CancellationToken.None);

                stopwatch.Stop();

                foreach (var camera in cameras)
                {
                    DiscoveredCameras.Add(camera);
                }

                DiscoveredCameraCount = DiscoveredCameras.Count;
                CameraSearchStatus = $"검색 완료 - {DiscoveredCameraCount}대 발견 ({stopwatch.Elapsed.TotalSeconds:F1}초)";
            }
            catch (Exception ex)
            {
                CameraSearchStatus = $"검색 오류: {ex.Message}";
                Debug.WriteLine($"[Dashboard] 카메라 검색 오류: {ex}");
            }
            finally
            {
                IsCameraScanning = false;
            }
        }

        public async Task LoadDevicesAsync()
        {
            if (_isInitialized)
                return;

            // await Task.Delay(5000); // 실제 장비 상태 가져오는 시뮬레이션

            var statuses = await _deviceService.GetAllStatusesAsync();
            Devices.Clear();
            foreach (var status in statuses)
                Devices.Add(status);

            _isInitialized = true;
        }

        public async Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                await LoadDevicesAsync();
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        [RelayCommand]
        private void PingTest()
        {
            if (SelectedDevice is null || string.IsNullOrWhiteSpace(SelectedDevice.IPString))
                return;

            string ip = SelectedDevice.IPString;
            LaunchPingShell(ip);
        }

        private void LaunchPingShell(string ip)
        {
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "Ping_Shell.ps1");

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoExit -ExecutionPolicy Bypass -File \"{scriptPath}\" -ip {ip}",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal,
            };

            Process.Start(psi);
        }


        /// <summary>
        /// 카메라 검색 이벤트 핸들러들
        /// </summary>
        private void OnCameraDiscovered(object? sender, CameraDiscoveredEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var existing = DiscoveredCameras.FirstOrDefault(c => c.IpAddress.Equals(e.Camera.IpAddress));
                if (existing != null)
                {
                    // 기존 카메라 정보 업데이트
                    existing.Status = e.Camera.Status;
                    existing.LastSeen = e.Camera.LastSeen;
                    existing.Version = e.Camera.Version;
                }
                else
                {
                    DiscoveredCameras.Add(e.Camera);
                }

                DiscoveredCameraCount = DiscoveredCameras.Count;
            });
        }

        private void OnDiscoveryProgress(object? sender, DiscoveryProgressEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CameraSearchStatus = $"검색 중... {e.TotalFound}대 발견 (경과: {e.ElapsedTime.TotalSeconds:F0}초)";
            });
        }

        private void OnDiscoveryCompleted(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!IsCameraScanning) return; // 이미 완료된 경우 무시

                IsCameraScanning = false;
                CameraSearchStatus = $"검색 완료 - {DiscoveredCameraCount}대 발견";
            });
        }
        /// <summary>
        /// 검색된 카메라를 기본 장비 목록에 추가
        /// </summary>
        [RelayCommand]
        private void AddDiscoveredCamerasToDevices()
        {
            foreach (var camera in DiscoveredCameras)
            {
                // 이미 존재하는지 확인
                var existing = Devices.FirstOrDefault(d => d.IPString == camera.IpAddressString);
                if (existing == null)
                {
                    var deviceStatus = new DeviceStatus
                    {
                        DeviceId = $"CAM_{camera.IpAddressString.Replace(".", "_")}",
                        Type = DeviceType.Camera,
                        IsOnline = camera.Status == CameraStatus.Online,
                        StatusMessage = camera.StatusText,
                        LastUpdated = camera.LastSeen,
                        IP = camera.IpAddress,
                        MacAddress = new MacAddress(camera.MacAddress),
                        Version = camera.Version
                    };

                    Devices.Add(deviceStatus);
                }
            }

            CameraSearchStatus = $"{DiscoveredCameras.Count}대의 카메라를 장비 목록에 추가했습니다.";
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public void Dispose()
        {
            if (_cameraDiscoveryService != null)
            {
                _cameraDiscoveryService.CameraDiscovered -= OnCameraDiscovered;
                _cameraDiscoveryService.DiscoveryProgress -= OnDiscoveryProgress;
                _cameraDiscoveryService.DiscoveryCompleted -= OnDiscoveryCompleted;
            }
        }

    }
}
