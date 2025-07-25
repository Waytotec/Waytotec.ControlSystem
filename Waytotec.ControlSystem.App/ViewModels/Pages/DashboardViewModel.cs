using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Core.Models;
using Wpf.Ui.Abstractions.Controls;

namespace Waytotec.ControlSystem.App.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, INavigationAware, INotifyPropertyChanged
    {
        private bool _isInitialized = false;
        private int _pingOffsetIndex = 0;
        private const int _maxWindowCount = 9;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [ObservableProperty]
        private DeviceStatus _selectedDevice;

        private bool _isScanning = false;
        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (_isScanning != value)
                {
                    _isScanning = value;
                    OnPropertyChanged();
                    ScanCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _scanStatus = "준비됨";
        public string ScanStatus
        {
            get => _scanStatus;
            set
            {
                if (_scanStatus != value)
                {
                    _scanStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<DeviceStatus> Devices { get; } = new();
        private readonly IDeviceService _deviceService;
        private readonly ICameraService _cameraService;

        public ObservableCollection<CameraInfo> Cameras { get; } = new();
        public IAsyncRelayCommand ScanCommand { get; }

        private string _title = "Waytotec Device Control System";

        public DashboardViewModel(IDeviceService deviceService, ICameraService cameraService)
        {
            _deviceService = deviceService;
            _cameraService = cameraService;

            ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);

            // 카메라 발견 이벤트 구독
            _cameraService.CameraFound += OnCameraFound;

            Debug.WriteLine("[DashboardViewModel] 생성자 완료, 이벤트 구독됨");
        }

        private void OnCameraFound(CameraInfo cameraInfo)
        {
            Debug.WriteLine($"[DashboardViewModel] 카메라 발견 이벤트 수신: {cameraInfo.Ip}");

            // UI 스레드에서 실행
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    // 중복 체크
                    if (!Cameras.Any(c => c.Ip == cameraInfo.Ip))
                    {
                        Cameras.Add(cameraInfo);
                        ScanStatus = $"검색 중... ({Cameras.Count}개 발견)";
                        Debug.WriteLine($"[DashboardViewModel] 카메라 추가됨: {cameraInfo.Ip}, 총 {Cameras.Count}개");
                    }
                    else
                    {
                        Debug.WriteLine($"[DashboardViewModel] 중복 카메라 무시: {cameraInfo.Ip}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DashboardViewModel] 카메라 추가 오류: {ex.Message}");
                }
            });
        }

        private async Task ScanAsync()
        {
            try
            {
                Debug.WriteLine("[DashboardViewModel] ScanAsync 시작");

                IsScanning = true;
                ScanStatus = "카메라 검색 준비 중...";

                // 기존 카메라 목록 클리어
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Cameras.Clear();
                    Debug.WriteLine("[DashboardViewModel] 기존 카메라 목록 클리어됨");
                });

                ScanStatus = "카메라 검색 시작...";

                // 스캔 시작
                bool scanStarted = await _cameraService.StartScanAsync();

                if (!scanStarted)
                {
                    ScanStatus = "스캔 시작 실패";
                    Debug.WriteLine("[DashboardViewModel] 스캔 시작 실패");
                    return;
                }

                Debug.WriteLine("[DashboardViewModel] 스캔 시작됨, 10초 대기");
                ScanStatus = "카메라 검색 중... (0개 발견)";

                // 10초간 대기 (스캔 진행)
                var scanTask = Task.Delay(10000);
                var progressTask = UpdateScanProgress();

                await Task.WhenAll(scanTask, progressTask);

                // 스캔 종료
                await _cameraService.StopScanAsync();
                Debug.WriteLine("[DashboardViewModel] 스캔 종료됨");

                ScanStatus = $"스캔 완료 - {Cameras.Count}개 발견";
                Debug.WriteLine($"[DashboardViewModel] 스캔 완료: {Cameras.Count}개 카메라 발견");
            }
            catch (Exception ex)
            {
                ScanStatus = $"스캔 오류: {ex.Message}";
                Debug.WriteLine($"[DashboardViewModel] 스캔 오류: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
                Debug.WriteLine("[DashboardViewModel] ScanAsync 완료");
            }
        }

        private async Task UpdateScanProgress()
        {
            int progress = 0;
            while (IsScanning && progress < 100)
            {
                await Task.Delay(100);
                progress += 1;

                if (progress % 10 == 0) // 1초마다 업데이트
                {
                    var currentCount = Cameras.Count;
                    ScanStatus = $"카메라 검색 중... ({progress / 10}초 경과, {currentCount}개 발견)";
                }
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

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
    }
}