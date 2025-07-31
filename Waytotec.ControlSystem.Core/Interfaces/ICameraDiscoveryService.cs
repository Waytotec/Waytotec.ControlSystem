using System.Net;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.Core.Interfaces
{
    /// <summary>
    /// 카메라 검색 서비스 인터페이스
    /// </summary>
    public interface ICameraDiscoveryService
    {
        /// <summary>
        /// 현재 검색 중인지 여부
        /// </summary>
        bool IsDiscovering { get; }

        /// <summary>
        /// 카메라 발견 이벤트
        /// </summary>
        event EventHandler<CameraDiscoveredEventArgs>? CameraDiscovered;

        /// <summary>
        /// 검색 진행률 이벤트
        /// </summary>
        event EventHandler<DiscoveryProgressEventArgs>? DiscoveryProgress;

        /// <summary>
        /// 검색 완료 이벤트
        /// </summary>
        event EventHandler<EventArgs>? DiscoveryCompleted;

        /// <summary>
        /// 비동기 카메라 검색 (단발성)
        /// </summary>
        /// <param name="timeout">검색 타임아웃</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>발견된 카메라 목록</returns>
        Task<IEnumerable<DiscoveredCamera>> DiscoverCamerasAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 지속적 검색 시작
        /// </summary>
        /// <param name="cancellationToken">취소 토큰</param>
        Task StartContinuousDiscoveryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 검색 중지
        /// </summary>
        Task StopDiscoveryAsync();

        /// <summary>
        /// IP 대역 검색 (메서드명 통일)
        /// </summary>
        /// <param name="startIp">시작 IP</param>
        /// <param name="endIp">종료 IP</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>발견된 카메라 목록</returns>
        Task<IEnumerable<DiscoveredCamera>> DiscoverCamerasInRangeAsync(
            IPAddress startIp,
            IPAddress endIp,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 단일 카메라 검증
        /// </summary>
        /// <param name="ipAddress">검증할 IP 주소</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>카메라 정보 (발견되지 않으면 null)</returns>
        Task<DiscoveredCamera?> VerifyCameraAsync(IPAddress ipAddress, CancellationToken cancellationToken = default);
    }
}