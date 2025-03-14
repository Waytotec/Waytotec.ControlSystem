using System;

namespace Waytotec.ControlSystem.Core.Models
{
    public class DeviceStatus
    {
        public string DeviceId { get; set; }
        public DeviceType Type { get; set; }
        public bool IsOnline { get; set; }
        public string StatusMessage { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
