using System.Collections.ObjectModel;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.App.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private DeviceStatus _selectedDevice;
        public DeviceStatus SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<DeviceStatus> Devices { get; } = new();
        private readonly IDeviceService _deviceService;


        private string _title = "Waytotec Control System";

        public MainViewModel(IDeviceService deviceService)
        {
            _deviceService = deviceService;
            // _ = LoadDeviceAsync();
        }

        public async Task LoadDevicesAsync()
        {
            await Task.Delay(5000); // 실제 장비 상태 가져오는 시뮬레이션

            var statuses = await _deviceService.GetAllStatusesAsync();
            Devices.Clear();
            foreach (var status in statuses)
                Devices.Add(status);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}
