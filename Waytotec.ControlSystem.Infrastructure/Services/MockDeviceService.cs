using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Waytotec.ControlSystem.Core.Models;
using Waytotec.ControlSystem.Core.Interfaces;

namespace Waytotec.ControlSystem.Infrastructure.Services
{
    public class MockDeviceService : IDeviceService
    {
        private List<DeviceStatus> _devices = new()
        {
            new DeviceStatus { DeviceId = "CAM001", Type = DeviceType.Camera, IsOnline = true, StatusMessage = "녹화 중", LastUpdated = DateTime.Now },
            new DeviceStatus { DeviceId = "BELL001", Type = DeviceType.EmergencyBell, IsOnline = false, StatusMessage = "오프라인", LastUpdated = DateTime.Now },
        };

        public Task<IEnumerable<DeviceStatus>> GetAllStatusesAsync()
            => Task.FromResult(_devices.AsEnumerable());

        public Task<DeviceStatus> GetStatusAsync(string deviceId)
            => Task.FromResult(_devices.FirstOrDefault(d => d.DeviceId == deviceId));

        public Task<bool> SendCommandAsync(string deviceId, string command)
        {
            var device = _devices.FirstOrDefault(d => d.DeviceId == deviceId);
            if (device != null)
            {
                device.StatusMessage = $"명령: {command}";
                device.LastUpdated = DateTime.Now;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
