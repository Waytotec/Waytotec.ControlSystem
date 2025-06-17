using System.Net;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.Infrastructure.Services
{
    public class MockDeviceService : IDeviceService
    {
        private List<DeviceStatus> _devices = new()
        {
            new DeviceStatus
            {
                DeviceId = "CAM001",
                Type = DeviceType.Camera,
                IsOnline = true,
                StatusMessage = "녹화 중",
                LastUpdated = DateTime.Now,
                IP = IPAddress.Parse("192.168.1.120"),
                MacAddress = new MacAddress("00-1A-2B-3C-4D-5E"),
                Version = "v1.0.0"
            },
            new DeviceStatus
            {
                DeviceId = "CAM002",
                Type = DeviceType.Camera,
                IsOnline = true,
                StatusMessage = "접속 중",
                LastUpdated = DateTime.Now,
                IP = IPAddress.Parse("192.168.1.122"),
                MacAddress = new MacAddress("02-3A-1B-2C-3D-0E"),
                Version = "v2.1.3"
            },
            new DeviceStatus
            {
                DeviceId = "CAM003",
                Type = DeviceType.Camera,
                IsOnline = false,
                StatusMessage = "연결 끊김",
                LastUpdated = DateTime.Now,
                IP = IPAddress.Parse("192.168.1.123"),
                MacAddress = new MacAddress("01-2A-3B-5C-2D-1E"),
                Version = "v3.0.1"
            },
            new DeviceStatus
            {
                DeviceId = "BELL001",
                Type = DeviceType.EmergencyBell,
                IsOnline = false,
                StatusMessage = "오프라인",
                LastUpdated = DateTime.Now,
                IP = IPAddress.Parse("192.168.1.121"),
                MacAddress = new MacAddress("00-1A-2B-3C-4D-5F"),
                Version = "v1.2.3"
            },
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
