using System.Net;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.Core.Interfaces
{
    /// <summary>
    /// 카메라 검색 서비스 인터페이스
    /// </summary
    public interface ICameraDiscoveryService
    {
        /// <summary>
        /// 카메라 검색 시작
        /// </summary>
        Task<IEnumerable<DiscoveredCamera>> DiscoverCamerasAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 실시간 카메라 검색 시작 (지속적 검색)
        /// </summary>
        Task StartContinuousDiscoveryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 검색 중지
        /// </summary>
        Task StopDiscoveryAsync();

        /// <summary>
        /// 특정 IP 대역 검색
        /// </summary>
        Task<IEnumerable<DiscoveredCamera>> DiscoverCamerasInRangeAsync(
            IPAddress startIp,
            IPAddress endIp,
            CancellationToken cancellationToken = default);


        /// <summary>
        /// IP 대역 검색
        /// </summary>
        /// <param name="startIp">시작 IP</param>
        /// <param name="endIp">종료 IP</param>
        /// <param name="timeout">타임아웃</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>발견된 카메라 목록</returns>
        Task<IEnumerable<DiscoveredCamera>> ScanNetworkRangeAsync(
            IPAddress startIp,
            IPAddress endIp,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 단일 카메라 검증
        /// </summary>
        Task<DiscoveredCamera?> VerifyCameraAsync(IPAddress ipAddress, CancellationToken cancellationToken = default);

        /// <summary>
        /// 카메라 발견 이벤트
        /// </summary>
        event EventHandler<CameraDiscoveredEventArgs>? CameraDiscovered;

        /// <summary>
        /// 검색 진행 상태 이벤트
        /// </summary>
        event EventHandler<DiscoveryProgressEventArgs>? DiscoveryProgress;

        /// <summary>
        /// 검색 완료 이벤트
        /// </summary>
        event EventHandler<EventArgs>? DiscoveryCompleted;

        /// <summary>
        /// 현재 검색 중인지 여부
        /// </summary>
        bool IsDiscovering { get; }
    }
}
