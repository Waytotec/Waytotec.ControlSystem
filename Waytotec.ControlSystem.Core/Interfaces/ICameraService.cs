using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.Core.Interfaces
{
    public interface ICameraService
    {
        IAsyncEnumerable<CameraInfo> FindCamerasAsync(CancellationToken token);
    }
}
