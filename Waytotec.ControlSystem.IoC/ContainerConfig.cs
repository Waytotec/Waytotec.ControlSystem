using Microsoft.Extensions.DependencyInjection;

namespace Waytotec.ControlSystem.IoC
{
    public static class ContainerConfig
    {
        public static ServiceProvider Configure(Action<IServiceCollection> extend = null)
        {
            var services = new ServiceCollection();

            // Core / Infrastructure 서비스 등록 예시
            // services.AddSingleton<IDeviceService, DeviceService>();

            extend?.Invoke(services); // 외부에서 DI 등록 확장

            return services.BuildServiceProvider();
        }
    }
}
