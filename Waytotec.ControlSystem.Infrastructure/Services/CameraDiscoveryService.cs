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
        private const int DEFAULT_TIMEOUT_MS = 10000;

        // WaytotekUpgrade 프로젝트와 동일한 검색 패킷 데이터
        private static readonly byte[] DISCOVERY_PACKET = {
            0x47, 0x43, 0x46, 0x47, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x43, 0x46, 0x47, 0x53, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        // WaytotekUpgrade 프로젝트와 동일한 구조체 정의
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WaytoCamStruct
        {
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
            public byte[] id1;

            public int version;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 8)]
            public byte[] mac;

            public uint ipAddress;
            public uint ipMask;
            public uint ipGateway;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
            public byte[] name;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
            public byte[] user;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
            public byte[] pass;

            public uint sig;
            public uint useWlan;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
            public byte[] ssid;

            public uint authtype;
            public uint keytype;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
            public byte[] key;

            public int http_port1;
            public int rtsp_port1;
            public int httpjpegport;
            public int ptzport;
        };

        private UdpClient? _udpClient;
        private bool _disposed = false;
        private bool _isDiscovering;
        private readonly object _lockObject = new();
        private CancellationTokenSource? _discoveryTokenSource;
        private readonly List<DiscoveredCamera> _discoveredCameras = new();

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

        /// <summary>
        /// 비동기 카메라 검색
        /// </summary>
        public async Task<IEnumerable<DiscoveredCamera>> DiscoverCamerasAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var timeoutMs = timeout?.TotalMilliseconds ?? DEFAULT_TIMEOUT_MS;
            var discoveredCameras = new List<DiscoveredCamera>();

            try
            {
                lock (_lockObject)
                {
                    if (_isDiscovering)
                    {
                        Debug.WriteLine("[CameraDiscovery] 이미 검색 중입니다.");
                        return discoveredCameras;
                    }
                    _isDiscovering = true;
                    _discoveredCameras.Clear();
                }

                Debug.WriteLine("[CameraDiscovery] 카메라 검색 시작");

                _discoveryTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _discoveryTokenSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

                await InitializeUdpClientAsync();

                var stopwatch = Stopwatch.StartNew();

                // 브로드캐스트로 검색 패킷 전송
                await SendBroadcastPacketAsync();

                // 진행률 업데이트 및 응답 수신을 동시에 실행
                var progressTask = UpdateProgressAsync(stopwatch, TimeSpan.FromMilliseconds(timeoutMs));
                var receiveTask = ReceiveResponsesAsync(_discoveryTokenSource.Token);

                // 모든 태스크 완료 대기
                await Task.WhenAll(progressTask, receiveTask);

                stopwatch.Stop();

                lock (_lockObject)
                {
                    discoveredCameras.AddRange(_discoveredCameras);
                }

                Debug.WriteLine($"[CameraDiscovery] 검색 완료: {discoveredCameras.Count}대 발견");
                DiscoveryCompleted?.Invoke(this, EventArgs.Empty);

                return discoveredCameras;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[CameraDiscovery] 검색이 취소되었습니다.");
                return discoveredCameras;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 검색 중 오류: {ex.Message}");
                throw;
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
        /// 지속적 검색 시작
        /// </summary>
        public async Task StartContinuousDiscoveryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_isDiscovering)
                    {
                        Debug.WriteLine("[CameraDiscovery] 이미 검색 중입니다.");
                        return;
                    }
                    _isDiscovering = true;
                }

                Debug.WriteLine("[CameraDiscovery] 지속적 검색 시작");

                _discoveryTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                await InitializeUdpClientAsync();

                var stopwatch = Stopwatch.StartNew();

                // 주기적으로 검색 패킷 전송 및 응답 수신
                var broadcastTask = PeriodicBroadcastAsync(_discoveryTokenSource.Token);
                var receiveTask = ReceiveResponsesAsync(_discoveryTokenSource.Token);
                var progressTask = UpdateProgressAsync(stopwatch, null);

                await Task.WhenAll(broadcastTask, receiveTask, progressTask);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[CameraDiscovery] 지속적 검색이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 지속적 검색 중 오류: {ex.Message}");
                throw;
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
        /// 검색 중지
        /// </summary>
        public async Task StopDiscoveryAsync()
        {
            try
            {
                _discoveryTokenSource?.Cancel();
                CleanupUdpClient();

                lock (_lockObject)
                {
                    _isDiscovering = false;
                }

                Debug.WriteLine("[CameraDiscovery] 검색이 중지되었습니다.");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 검색 중지 중 오류: {ex.Message}");
            }
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

            try
            {
                Debug.WriteLine($"[CameraDiscovery] IP 대역 검색: {startIp} - {endIp}");

                var startBytes = startIp.GetAddressBytes();
                var endBytes = endIp.GetAddressBytes();
                var startInt = BitConverter.ToUInt32(startBytes.Reverse().ToArray(), 0);
                var endInt = BitConverter.ToUInt32(endBytes.Reverse().ToArray(), 0);

                const int maxConcurrent = 50; // 동시 검색 제한

                for (uint ip = startInt; ip <= endInt; ip++)
                {
                    var ipBytes = BitConverter.GetBytes(ip).Reverse().ToArray();
                    var ipAddress = new IPAddress(ipBytes);

                    tasks.Add(VerifyCameraAsync(ipAddress, cancellationToken));

                    if (tasks.Count >= maxConcurrent)
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] IP 대역 검색 오류: {ex.Message}");
                throw;
            }
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
                await client.SendAsync(DISCOVERY_PACKET, DISCOVERY_PACKET.Length, endPoint);

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

            Debug.WriteLine($"[CameraDiscovery] UDP 클라이언트 초기화 완료: 포트 {DISCOVERY_PORT}");
            await Task.Delay(100); // 초기화 시간 확보
        }

        /// <summary>
        /// 브로드캐스트 패킷 전송
        /// </summary>
        private async Task SendBroadcastPacketAsync()
        {
            try
            {
                if (_udpClient == null) return;

                var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
                await _udpClient.SendAsync(DISCOVERY_PACKET, DISCOVERY_PACKET.Length, broadcastEndPoint);

                Debug.WriteLine("[CameraDiscovery] 브로드캐스트 패킷 전송됨");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 브로드캐스트 전송 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 주기적 브로드캐스트 (지속적 검색용)
        /// </summary>
        private async Task PeriodicBroadcastAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await SendBroadcastPacketAsync();
                    await Task.Delay(3000, cancellationToken); // 3초 간격
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 주기적 브로드캐스트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 응답 수신 루프 (수정된 버전)
        /// </summary>
        private async Task ReceiveResponsesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _udpClient != null)
                {
                    try
                    {
                        // 비동기적으로 UDP 응답 대기 (타임아웃 설정)
                        var receiveTask = _udpClient.ReceiveAsync();
                        var timeoutTask = Task.Delay(500, cancellationToken); // 500ms 타임아웃

                        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

                        if (completedTask == receiveTask)
                        {
                            var result = await receiveTask;
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
                        // 타임아웃인 경우 루프 계속 진행
                    }
                    catch (ObjectDisposedException)
                    {
                        break; // UDP 클라이언트가 해제됨
                    }
                    catch (SocketException ex)
                    {
                        Debug.WriteLine($"[CameraDiscovery] 소켓 오류: {ex.Message}");
                        await Task.Delay(100, cancellationToken); // 잠시 대기 후 재시도
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CameraDiscovery] 수신 오류: {ex.Message}");
                        await Task.Delay(100, cancellationToken); // 잠시 대기 후 재시도
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소
                Debug.WriteLine("[CameraDiscovery] 응답 수신이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 수신 루프 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 진행률 업데이트 (수정된 버전)
        /// </summary>
        private async Task UpdateProgressAsync(Stopwatch stopwatch, TimeSpan? totalDuration)
        {
            try
            {
                while (!_discoveryTokenSource?.Token.IsCancellationRequested == true)
                {
                    var elapsed = stopwatch.Elapsed;

                    // 발견된 카메라 수를 포함한 진행률 이벤트
                    int discoveredCount;
                    lock (_lockObject)
                    {
                        discoveredCount = _discoveredCameras.Count;
                    }

                    var progress = new DiscoveryProgressEventArgs(elapsed, discoveredCount);
                    DiscoveryProgress?.Invoke(this, progress);

                    await Task.Delay(200, _discoveryTokenSource.Token); // 200ms마다 업데이트
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 진행률 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 카메라 응답 파싱
        /// </summary>
        private DiscoveredCamera? ParseCameraResponse(byte[] data, EndPoint remoteEndPoint)
        {
            try
            {
                if (data.Length < Marshal.SizeOf<WaytoCamStruct>())
                    return null;

                var camera = BytesToStructure<WaytoCamStruct>(data, 0);

                // MAC 주소 유효성 검사 (WaytotekUpgrade 프로젝트와 동일한 로직)
                bool isValidMac = false;
                for (int i = 0; i < 6 && i < camera.mac.Length; i++)
                {
                    if (camera.mac[i] != 0xff)
                    {
                        isValidMac = true;
                        break;
                    }
                }

                if (!isValidMac && camera.http_port1 == 0)
                    return null;

                var ipBytes = BitConverter.GetBytes(camera.ipAddress);
                var ipAddress = new IPAddress(ipBytes);

                // MAC 주소 문자열 생성
                var macAddress = camera.mac.Length >= 6
                    ? string.Join(":", camera.mac.Take(6).Select(b => b.ToString("X2")))
                    : "00:00:00:00:00:00";

                // 시리얼 번호 추출
                var serialNumber = ExtractStringFromBytes(camera.name);

                var discoveredCamera = new DiscoveredCamera
                {
                    IpAddress = ipAddress,
                    IpAddressString = ipAddress.ToString(),
                    MacAddress = macAddress,
                    SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? "N/A" : serialNumber,
                    HttpPort = camera.http_port1 > 0 ? camera.http_port1 : 80,
                    RtspPort = camera.rtsp_port1 > 0 ? camera.rtsp_port1 : 554,
                    Version = camera.version.ToString(),
                    Status = CameraStatus.Online,
                    LastSeen = DateTime.Now
                };

                return discoveredCamera;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 응답 파싱 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 바이트 배열을 구조체로 변환 (WaytotekUpgrade 프로젝트와 동일한 방법)
        /// </summary>
        private static T BytesToStructure<T>(byte[] data, int offset, int size = 0)
        {
            if (size == 0)
                size = Marshal.SizeOf(typeof(T));

            T structure = (T)Activator.CreateInstance(typeof(T))!;
            IntPtr ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.Copy(data, offset, ptr, size);
                structure = (T)Marshal.PtrToStructure(ptr, structure!.GetType())!;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return structure;
        }

        /// <summary>
        /// 바이트 배열에서 문자열 추출
        /// </summary>
        private static string ExtractStringFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            // null 종료자까지만 읽기
            var nullIndex = Array.IndexOf(bytes, (byte)0);
            var length = nullIndex >= 0 ? nullIndex : bytes.Length;

            return Encoding.ASCII.GetString(bytes, 0, length).Trim();
        }

        /// <summary>
        /// UDP 클라이언트 정리
        /// </summary>
        private void CleanupUdpClient()
        {
            try
            {
                _udpClient?.Close();
                _udpClient?.Dispose();
                _udpClient = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] UDP 클라이언트 정리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 리소스 해제
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _discoveryTokenSource?.Cancel();
                _discoveryTokenSource?.Dispose();
                CleanupUdpClient();
                _disposed = true;

                Debug.WriteLine("[CameraDiscovery] 리소스가 해제되었습니다.");
            }
        }
    }
}