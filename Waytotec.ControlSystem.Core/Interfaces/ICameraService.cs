using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.Core.Interfaces
{
    public interface ICameraService
    {
        IAsyncEnumerable<CameraInfo> FindCamerasAsync(CancellationToken token);
        Task<bool> StartScanAsync();
        Task StopScanAsync();
        bool IsScanning { get; }
        event Action<CameraInfo> CameraFound;

    }
}
