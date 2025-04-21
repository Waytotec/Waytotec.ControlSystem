using System.Net;

namespace Waytotec.ControlSystem.Core.Models
{
    public class DeviceStatus
    {
        public string? DeviceId { get; set; }
        public DeviceType Type { get; set; }
        public bool IsOnline { get; set; }
        public string? StatusMessage { get; set; }
        public DateTime LastUpdated { get; set; }

        public IPAddress? IP { get; set; }
        public MacAddress? MacAddress { get; set; }
        public string IPString => IP?.ToString() ?? string.Empty;
        public string MacAddressString => MacAddress?.ToString() ?? string.Empty;

        public string? Version { get; set; }
    }
}
