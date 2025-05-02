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


        private string _title = "Waytotec Device Control System";

        public DashboardViewModel(IDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        public async Task LoadDevicesAsync()
        {
            if (_isInitialized)
                return;

            await Task.Delay(5000); // 실제 장비 상태 가져오는 시뮬레이션

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
    }
}
