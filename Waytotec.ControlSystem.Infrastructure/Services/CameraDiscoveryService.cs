using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
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
                Debug.WriteLine("[CameraDiscovery] 단발성 검색 시작");

                await InitializeUdpClientAsync();

                // 모든 네트워크 인터페이스에 브로드캐스트 전송
                await SendBroadcastToAllInterfacesAsync();

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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 검색 중지 중 오류: {ex.Message}");
            }
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
        /// 모든 네트워크 인터페이스에 브로드캐스트 전송
        /// </summary>
        private async Task SendBroadcastToAllInterfacesAsync()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var tasks = new List<Task>();

            Debug.WriteLine("[CameraDiscovery] 네트워크 인터페이스 검색 중...");

            foreach (var networkInterface in networkInterfaces)
            {
                // 활성화된 네트워크 인터페이스만 처리
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                // 루프백 인터페이스 제외
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                Debug.WriteLine($"[CameraDiscovery] 활성 인터페이스: {networkInterface.Name} ({networkInterface.NetworkInterfaceType})");

                var ipProperties = networkInterface.GetIPProperties();

                foreach (var unicastAddress in ipProperties.UnicastAddresses)
                {
                    // IPv4 주소만 처리
                    if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    // APIPA 주소 제외 (169.254.x.x)
                    if (unicastAddress.Address.ToString().StartsWith("169.254"))
                        continue;

                    Debug.WriteLine($"[CameraDiscovery] 유효한 IPv4 주소: {unicastAddress.Address} / {unicastAddress.IPv4Mask}");
                    tasks.Add(SendBroadcastOnInterfaceAsync(unicastAddress));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
                Debug.WriteLine($"[CameraDiscovery] {tasks.Count}개 인터페이스에 브로드캐스트 전송 완료");
            }
            else
            {
                Debug.WriteLine("[CameraDiscovery] 경고: 브로드캐스트 가능한 인터페이스를 찾을 수 없습니다. 기본 브로드캐스트 시도");
                await SendFallbackBroadcastAsync();
            }
        }

        /// <summary>
        /// 특정 네트워크 인터페이스에서 브로드캐스트 전송
        /// </summary>
        private async Task SendBroadcastOnInterfaceAsync(UnicastIPAddressInformation unicastAddress)
        {
            UdpClient? udpClient = null;
            try
            {
                var localAddress = unicastAddress.Address;
                var subnetMask = unicastAddress.IPv4Mask;

                // 브로드캐스트 주소 계산
                var broadcastAddress = GetBroadcastAddress(localAddress, subnetMask);

                Debug.WriteLine($"[CameraDiscovery] {localAddress}에서 {broadcastAddress}로 브로드캐스트");

                // 각 인터페이스별로 별도의 UDP 클라이언트 생성
                udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                udpClient.ExclusiveAddressUse = false;

                // 특정 로컬 주소에 바인딩
                var localEndPoint = new IPEndPoint(localAddress, 0); // 0 = 자동 포트 할당
                udpClient.Client.Bind(localEndPoint);

                // 서브넷 브로드캐스트 전송
                var broadcastEndPoint = new IPEndPoint(broadcastAddress, DISCOVERY_PORT);
                await udpClient.SendAsync(DISCOVERY_PACKET, DISCOVERY_PACKET.Length, broadcastEndPoint);

                // 전역 브로드캐스트도 전송
                var globalBroadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
                await udpClient.SendAsync(DISCOVERY_PACKET, DISCOVERY_PACKET.Length, globalBroadcastEndPoint);

                Debug.WriteLine($"[CameraDiscovery] {localAddress}에서 브로드캐스트 전송 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 인터페이스 {unicastAddress.Address}에서 브로드캐스트 실패: {ex.Message}");
            }
            finally
            {
                udpClient?.Close();
                udpClient?.Dispose();
            }
        }

        /// <summary>
        /// 폴백 브로드캐스트 전송
        /// </summary>
        private async Task SendFallbackBroadcastAsync()
        {
            try
            {
                Debug.WriteLine("[CameraDiscovery] 폴백 브로드캐스트 시도");
                using var udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
                await udpClient.SendAsync(DISCOVERY_PACKET, DISCOVERY_PACKET.Length, broadcastEndPoint);

                Debug.WriteLine("[CameraDiscovery] 폴백 브로드캐스트 전송 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 폴백 브로드캐스트 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 브로드캐스트 주소 계산
        /// </summary>
        private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            if (subnetMask == null)
                return IPAddress.Broadcast;

            var addressBytes = address.GetAddressBytes();
            var subnetMaskBytes = subnetMask.GetAddressBytes();
            var broadcastBytes = new byte[addressBytes.Length];

            for (int i = 0; i < addressBytes.Length; i++)
            {
                broadcastBytes[i] = (byte)(addressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }

            return new IPAddress(broadcastBytes);
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
        /// 주기적 브로드캐스트 (지속적 검색용)
        /// </summary>
        private async Task PeriodicBroadcastAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await SendBroadcastToAllInterfacesAsync();
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
        /// 응답 수신 (지정된 시간동안)
        /// </summary>
        private async Task ReceiveResponsesAsync(double timeoutMs, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            // 수신 태스크와 진행률 업데이트를 분리
            var receiveTask = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested && _udpClient != null)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync();
                        Debug.WriteLine($"[CameraDiscovery] {result.RemoteEndPoint}에서 {result.Buffer.Length} 바이트 수신");

                        var camera = ParseCameraResponse(result.Buffer, result.RemoteEndPoint);
                        if (camera != null)
                        {
                            lock (_lockObject)
                            {
                                var existing = _discoveredCameras.FirstOrDefault(c => c.IpAddress.Equals(camera.IpAddress));
                                if (existing != null)
                                {
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
                        break;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CameraDiscovery] 수신 오류: {ex.Message}");
                        await Task.Delay(100, cancellationToken);
                    }
                }
            }, cancellationToken);

            // 진행률 업데이트
            while (stopwatch.ElapsedMilliseconds < timeoutMs && !cancellationToken.IsCancellationRequested)
            {
                lock (_lockObject)
                {
                    var progressPercent = (int)((stopwatch.ElapsedMilliseconds / timeoutMs) * 100);
                    var progress = new DiscoveryProgressEventArgs(                        
                        stopwatch.Elapsed,
                        _discoveredCameras.Count
                        );
                    DiscoveryProgress?.Invoke(this, progress);
                }

                await Task.Delay(200, cancellationToken); // 0.2초마다 업데이트
            }

            // 완료 진행률
            lock (_lockObject)
            {
                var finalProgress = new DiscoveryProgressEventArgs(
                    stopwatch.Elapsed,
                    _discoveredCameras.Count);
                DiscoveryProgress?.Invoke(this, finalProgress);
            }
        }

        /// <summary>
        /// 지속적 응답 수신
        /// </summary>
        private async Task ReceiveResponsesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _udpClient != null)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync();
                        Debug.WriteLine($"[CameraDiscovery] {result.RemoteEndPoint}에서 {result.Buffer.Length} 바이트 수신");

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
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                    {
                        break; // 정상적인 중단
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CameraDiscovery] 수신 오류: {ex.Message}");
                        await Task.Delay(100, cancellationToken); // 짧은 지연 후 재시도
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraDiscovery] 응답 수신 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 진행률 업데이트
        /// </summary>
        private async Task UpdateProgressAsync(Stopwatch stopwatch, double? totalTimeoutMs)
        {
            try
            {
                //while (stopwatch.IsRunning)
                while (stopwatch.IsRunning && (_discoveryTokenSource == null || !_discoveryTokenSource.Token.IsCancellationRequested))
                {
                    lock (_lockObject)
                    {
                        var progress = new DiscoveryProgressEventArgs(
                            stopwatch.Elapsed, 
                            _discoveredCameras.Count
                            );
                        DiscoveryProgress?.Invoke(this, progress);
                    }
                    await Task.Delay(500); // 0.5초마다 업데이트

                    if (totalTimeoutMs.HasValue && stopwatch.ElapsedMilliseconds >= totalTimeoutMs.Value)
                        break;
                }
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
                    HttpJpegPort = camera.httpjpegport > 0 ? camera.httpjpegport : 8080,
                    PtzPort = camera.ptzport > 0 ? camera.ptzport : 0,
                    Version = camera.version.ToString(),
                    Status = CameraStatus.Online,
                    LastSeen = DateTime.Now,
                    SubnetMask = new IPAddress(BitConverter.GetBytes(camera.ipMask)),
                    Gateway = new IPAddress(BitConverter.GetBytes(camera.ipGateway))
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
                if (_udpClient == null)
                    return;

                _udpClient.Close();
                _udpClient.Dispose();
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