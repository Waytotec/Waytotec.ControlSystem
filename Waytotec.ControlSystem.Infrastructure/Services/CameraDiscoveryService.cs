using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.Infrastructure.Services
{
    public class CameraDiscoveryService : ICameraDiscoveryService, IDisposable
    {
        private const int DISCOVERY_PORT = 20011;
        private const int FIRMWARE_PORT = 7061;
        private const int DISCOVERY_TIMEOUT = 10000;
        private const uint CFG_SIGNATURE = 0x53474643; // 'CFGS'
        private const uint MSG_GETCONFIG = 0x47464347; // 'GCFG' 
        private const uint MSG_GETCONFIGOK = 0x4B4F4347; // 'GCOK'
        private const int DEFAULT_TIMEOUT_MS = 5000;

        private UdpClient? _udpClient;
        private bool _disposed = false;
        private bool _isDiscovering;
        private readonly object _lockObject = new();
        private CancellationTokenSource? _discoveryTokenSource;
        private readonly List<DiscoveredCamera> _discoveredCameras = new();
        private readonly byte[] _discoveryPacket;

        public bool IsDiscovering
        {
            get
            {
                lock (_lockObject)
                {
                    return _isDiscovering;
                }
            }
        }

        public event EventHandler<CameraDiscoveredEventArgs>? CameraDiscovered;
        public event EventHandler<DiscoveryProgressEventArgs>? DiscoveryProgress;
        public event EventHandler<EventArgs>? DiscoveryCompleted;

        public CameraDiscoveryService()
        {
            _discoveryPacket = CreateDiscoveryPacket();
        }

        /// <summary>
        /// 카메라 검색 패킷 생성 (브로드캐스트용)
        /// </summary>
        private byte[] CreateDiscoveryPacket()
        {
            var message = new CameraDiscoveryMessage
            {
                Message = MSG_GETCONFIG,
                Version = 1,
                ConfigData = new CameraNvRamStruct
                {
                    MacAddress = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff },
                    Signature = CFG_SIGNATURE
                }
            };

            int size = Marshal.SizeOf<CameraDiscoveryMessage>();
            byte[] packet = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(message, ptr, false);
                Marshal.Copy(ptr, packet, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return packet;
        }

        /// <summary>
        /// 비동기 카메라 검색
        /// </summary>
        public async Task<IEnumerable<DiscoveredCamera>> DiscoverCamerasAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var timeoutMs = timeout?.TotalMilliseconds ?? DEFAULT_TIMEOUT_MS;
            var stopwatch = Stopwatch.StartNew();

            lock (_lockObject)
            {
                if (_isDiscovering)
                    throw new InvalidOperationException("이미 검색이 진행 중입니다.");

                _isDiscovering = true;
                _discoveredCameras.Clear();
            }

            try
            {
                await InitializeUdpClientAsync();

                // 브로드캐스트 전송
                await SendDiscoveryBroadcastAsync();

                // 응답 수신
                await ReceiveResponsesAsync(timeoutMs, stopwatch, cancellationToken);

                lock (_lockObject)
                {
                    DiscoveryCompleted?.Invoke(this, EventArgs.Empty);
                    return _discoveredCameras.ToList();
                }
            }
            finally
            {
                lock (_lockObject)
                {
                    _isDiscovering = false;
                }
                CleanupUdpClient();
            }
        }

        /// <summary>
        /// 실시간 지속적 검색
        /// </summary>
        public async Task StartContinuousDiscoveryAsync(CancellationToken cancellationToken = default)
        {
            lock (_lockObject)
            {
                if (_isDiscovering)
                    return;

                _isDiscovering = true;
                _discoveryTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            try
            {
                await InitializeUdpClientAsync();

                while (!_discoveryTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        await SendDiscoveryBroadcastAsync();
                        await Task.Delay(1000, _discoveryTokenSource.Token); // 1초마다 브로드캐스트
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                await StopDiscoveryAsync();
            }
        }

        /// <summary>
        /// 검색 중지
        /// </summary>
        public async Task StopDiscoveryAsync()
        {
            lock (_lockObject)
            {
                _isDiscovering = false;
                _discoveryTokenSource?.Cancel();
            }

            CleanupUdpClient();
            await Task.Delay(100); // 정리 시간 확보
        }

        /// <summary>
        /// IP 대역 검색
        /// </summary>
        public async Task<IEnumerable<DiscoveredCamera>> DiscoverCamerasInRangeAsync(
            IPAddress startIp,
            IPAddress endIp,
            CancellationToken cancellationToken = default)
        {
            var cameras = new List<DiscoveredCamera>();
            var tasks = new List<Task<DiscoveredCamera?>>();

            // IP 범위 내의 모든 IP에 대해 개별 검색
            var start = BitConverter.ToUInt32(startIp.GetAddressBytes().Reverse().ToArray(), 0);
            var end = BitConverter.ToUInt32(endIp.GetAddressBytes().Reverse().ToArray(), 0);

            for (uint ip = start; ip <= end; ip++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var ipBytes = BitConverter.GetBytes(ip).Reverse().ToArray();
                var targetIp = new IPAddress(ipBytes);

                tasks.Add(VerifyCameraAsync(targetIp, cancellationToken));

                // 동시 실행 제한 (50개씩)
                if (tasks.Count >= 50)
                {
                    var results = await Task.WhenAll(tasks);
                    cameras.AddRange(results.Where(c => c != null)!);
                    tasks.Clear();
                }
            }

            if (tasks.Count > 0)
            {
                var results = await Task.WhenAll(tasks);
                cameras.AddRange(results.Where(c => c != null)!);
            }

            return cameras;
        }

        /// <summary>
        /// 단일 카메라 검증
        /// </summary>
        public async Task<DiscoveredCamera?> VerifyCameraAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = new UdpClient();
                client.Client.ReceiveTimeout = 2000; // 2초 타임아웃

                var endPoint = new IPEndPoint(ipAddress, DISCOVERY_PORT);
                await client.SendAsync(_discoveryPacket, _discoveryPacket.Length, endPoint);

                var result = await client.ReceiveAsync();
                return ParseCameraResponse(result.Buffer, result.RemoteEndPoint);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// UDP 클라이언트 초기화
        /// </summary>
        private async Task InitializeUdpClientAsync()
        {
            CleanupUdpClient();

            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            _udpClient.ExclusiveAddressUse = false;

            var localEndPoint = new IPEndPoint(IPAddress.Any, DISCOVERY_PORT);
            _udpClient.Client.Bind(localEndPoint);

            // 백그라운드에서 응답 수신 시작
            _ = Task.Run(ContinuousReceiveAsync);

            await Task.Delay(100); // 초기화 시간 확보
        }

        /// <summary>
        /// 브로드캐스트 전송
        /// </summary>
        private async Task SendDiscoveryBroadcastAsync()
        {
            if (_udpClient == null) return;

            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
            await _udpClient.SendAsync(_discoveryPacket, _discoveryPacket.Length, broadcastEndPoint);

            Debug.WriteLine($"[CameraDiscovery] 브로드캐스트 전송: {_discoveryPacket.Length} bytes");
        }

        /// <summary>
        /// 응답 수신 (지정된 시간동안)
        /// </summary>
        private async Task ReceiveResponsesAsync(double timeoutMs, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            while (stopwatch.ElapsedMilliseconds < timeoutMs && !cancellationToken.IsCancellationRequested)
            {
                lock (_lockObject)
                {
                    var progress = new DiscoveryProgressEventArgs(
                        _discoveredCameras.Count,
                        stopwatch.Elapsed);
                    DiscoveryProgress?.Invoke(this, progress);
                }

                await Task.Delay(100, cancellationToken);
            }
        }

        /// <summary>
        /// 지속적 응답 수신 (백그라운드)
        /// </summary>
        private async Task ContinuousReceiveAsync()
        {
            while (_udpClient != null && IsDiscovering)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    var camera = ParseCameraResponse(result.Buffer, result.RemoteEndPoint);

                    if (camera != null)
                    {
                        lock (_lockObject)
                        {
                            // 중복 체크 (IP 기준)
                            var existing = _discoveredCameras.FirstOrDefault(c =>
                                c.IpAddress.Equals(camera.IpAddress));

                            if (existing != null)
                            {
                                // 기존 정보 업데이트
                                existing.LastSeen = DateTime.Now;
                                existing.Status = camera.Status;
                            }
                            else
                            {
                                _discoveredCameras.Add(camera);
                            }
                        }

                        CameraDiscovered?.Invoke(this, new CameraDiscoveredEventArgs(camera, result.RemoteEndPoint));
                        Debug.WriteLine($"[CameraDiscovery] 카메라 발견: {camera.IpAddressString} ({camera.MacAddress})");
                    }
                }
                catch (ObjectDisposedException)
                {
                    break; // UDP 클라이언트가 해제됨
                }
                catch (SocketException ex)
                {
                    Debug.WriteLine($"[CameraDiscovery] 소켓 오류: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CameraDiscovery] 수신 오류: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 카메라 응답 파싱
        /// </summary>
        private DiscoveredCamera? ParseCameraResponse(byte[] buffer, IPEndPoint remoteEndPoint)
        {
            try
            {
                if (buffer.Length < Marshal.SizeOf<CameraDiscoveryMessage>())
                    return null;

                var response = BytesToStructure<CameraDiscoveryMessage>(buffer);

                // 응답 메시지 검증
                if (response.Message != MSG_GETCONFIGOK || response.ConfigData.Signature != CFG_SIGNATURE)
                    return null;

                var config = response.ConfigData;

                // MAC 주소가 유효한지 확인
                if (config.MacAddress == null || config.MacAddress.All(b => b == 0xff))
                    return null;

                var camera = new DiscoveredCamera
                {
                    Id = $"CAM_{remoteEndPoint.Address}",
                    IpAddress = new IPAddress(BitConverter.GetBytes(config.IpAddress)),
                    MacAddress = BitConverter.ToString(config.MacAddress).Replace("-", ""),
                    SerialNumber = Encoding.ASCII.GetString(config.TargetName).TrimEnd('\0'),
                    Version = "Unknown", // 별도 요청으로 획득
                    Status = CameraStatus.Online,
                    SubnetMask = new IPAddress(BitConverter.GetBytes(config.IpMask)),
                    Gateway = new IPAddress(BitConverter.GetBytes(config.IpGateway)),
                    HttpPort = (int)config.HttpPort,
                    RtspPort = (int)config.RtspPort,
                    HttpJpegPort = (int)config.HttpJpegPort,
                    PtzPort = (int)config.PtzPort,
                    LastSeen = DateTime.Now
                };

                return camera;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 응답 파싱 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 바이트 배열을 구조체로 변환
        /// </summary>
        private static T BytesToStructure<T>(byte[] bytes) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            if (bytes.Length < size)
                throw new ArgumentException("바이트 배열 크기가 구조체보다 작습니다.");

            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(bytes, 0, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// UDP 클라이언트 정리
        /// </summary>
        private void CleanupUdpClient()
        {
            try
            {
                if (_udpClient != null)
                {
                    _udpClient.Close();
                    _udpClient.Dispose();
                    _udpClient = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] UDP 클라이언트 정리 오류: {ex.Message}");
            }
            finally
            {
                _udpClient = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                lock (_lockObject)
                {
                    _disposed = true;
                }

                Debug.WriteLine("[CameraDiscoveryService] Dispose 시작");

                // 1. CancellationToken 즉시 취소
                _discoveryTokenSource?.Cancel();

                // 2.UDP 소켓 강제 종료
                CleanupUdpClient();

                // 3. TokenSource 정리
                try
                {
                    _discoveryTokenSource?.Dispose();
                    _discoveryTokenSource = null;
                    Debug.WriteLine("TokenSource 정리 완료");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TokenSource 정리 오류: {ex.Message}");
                }

                Debug.WriteLine("[CameraDiscoveryService] Dispose 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscoveryService] Dispose 오류: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }
    }
}
