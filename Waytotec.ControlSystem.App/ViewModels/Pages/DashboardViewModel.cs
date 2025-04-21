using System.Collections.ObjectModel;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Core.Models;
using Wpf.Ui.Abstractions.Controls;

namespace Waytotec.ControlSystem.App.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

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
    }
}
