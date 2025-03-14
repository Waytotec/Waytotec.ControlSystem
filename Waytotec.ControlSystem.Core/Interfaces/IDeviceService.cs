using System.Collections.Generic;
using System.Threading.Tasks;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.Core.Interfaces
{
    public interface IDeviceService
    {
        Task<IEnumerable<DeviceStatus>> GetAllStatusesAsync();
        Task<DeviceStatus> GetStatusAsync(string deviceId);
        Task<bool> SendCommandAsync(string deviceId, string command);
    }
}
