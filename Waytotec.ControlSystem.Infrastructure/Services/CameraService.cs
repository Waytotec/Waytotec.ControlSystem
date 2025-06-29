using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Waytotec.ControlSystem.Core.Interfaces;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.Infrastructure.Services
{
    public class CameraService : ICameraService, IDisposable
    {
        private UdpClient? _udpClient;
        private bool _isScanning = false;
        private CancellationTokenSource? _scanCts;
        private readonly object _lockObject = new object();

        // WaytotekUpgrade 프로젝트의 검색 패킷 데이터
        private static readonly byte[] SearchPacket = {
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

        private const int PORT_NUMBER = 20011;

        public bool IsScanning
        {
            get
            {
                lock (_lockObject)
                {
                    return _isScanning;
                }
            }
        }

        public event Action<CameraInfo>? CameraFound;

        public async Task<bool> StartScanAsync()
        {
            lock (_lockObject)
            {
                if (_isScanning)
                {
                    Debug.WriteLine("[CameraService] 이미 스캔 중입니다.");
                    return false;
                }

                _isScanning = true;
            }

            try
            {
                Debug.WriteLine("[CameraService] 스캔 시작 시도...");
                _scanCts = new CancellationTokenSource();

                // UDP 클라이언트 초기화
                _udpClient = new UdpClient();
                var localEndPoint = new IPEndPoint(IPAddress.Any, PORT_NUMBER);

                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                _udpClient.ExclusiveAddressUse = false;
                _udpClient.Client.Bind(localEndPoint);

                Debug.WriteLine($"[CameraService] UDP 클라이언트 바인딩 완료: 포트 {PORT_NUMBER}");

                // 수신 대기 시작
                _ = Task.Run(async () => await ReceiveLoopAsync(_scanCts.Token), _scanCts.Token);
                Debug.WriteLine("[CameraService] 수신 루프 시작됨");

                // 검색 패킷 전송
                await SendSearchPacketAsync();
                Debug.WriteLine("[CameraService] 검색 패킷 전송 완료");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraService] 검색 시작 실패: {ex.Message}");
                Debug.WriteLine($"[CameraService] 스택 추적: {ex.StackTrace}");
                await StopScanAsync();
                return false;
            }
        }

        public async Task StopScanAsync()
        {
            lock (_lockObject)
            {
                if (!_isScanning)
                    return;

                _isScanning = false;
            }

            try
            {
                _scanCts?.Cancel();
                _udpClient?.Close();
                _udpClient?.Dispose();
                _udpClient = null;
                _scanCts?.Dispose();
                _scanCts = null;

                Debug.WriteLine("[CameraService] 스캔 중지됨");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraService] 스캔 중지 오류: {ex.Message}");
            }
        }

        private async Task SendSearchPacketAsync()
        {
            try
            {
                if (_udpClient == null) return;

                var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, PORT_NUMBER);
                await _udpClient.SendAsync(SearchPacket, SearchPacket.Length, broadcastEndPoint);

                Debug.WriteLine("[CameraService] 검색 패킷 전송됨");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraService] 검색 패킷 전송 실패: {ex.Message}");
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _udpClient != null)
                {
                    var result = await _udpClient.ReceiveAsync();
                    ProcessReceivedData(result.Buffer, result.RemoteEndPoint.Address.ToString());
                }
            }
            catch (ObjectDisposedException)
            {
                // UDP 클라이언트가 정리된 경우 - 정상적인 종료
                Debug.WriteLine("[CameraService] UDP 클라이언트 정리됨");
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"[CameraService] 소켓 예외: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraService] 수신 루프 예외: {ex.Message}");
            }
        }

        private void ProcessReceivedData(byte[] data, string sourceIP)
        {
            try
            {
                if (data.Length < 120) // _waytocam 구조체 최소 크기 체크
                    return;

                var cameraInfo = ParseCameraData(data, sourceIP);
                if (cameraInfo != null)
                {
                    Debug.WriteLine($"[CameraService] 카메라 발견: {sourceIP}");

                    // 버전 정보 비동기 획득
                    _ = Task.Run(async () =>
                    {
                        cameraInfo.Version = await GetCameraVersionAsync(sourceIP);
                        CameraFound?.Invoke(cameraInfo);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraService] 데이터 처리 오류: {ex.Message}");
            }
        }

        private static CameraInfo? ParseCameraData(byte[] data, string sourceIP)
        {
            try
            {
                // MAC 주소 추출 (오프셋 4, 6바이트)
                var macBytes = new byte[6];
                Array.Copy(data, 4, macBytes, 0, 6);

                // 유효한 MAC 주소인지 확인
                bool isValidMac = false;
                for (int i = 0; i < 6; i++)
                {
                    if (macBytes[i] != 0xFF)
                    {
                        isValidMac = true;
                        break;
                    }
                }

                if (!isValidMac)
                    return null;

                // IP 주소 추출 (오프셋 14, 4바이트)
                var ipBytes = new byte[4];
                Array.Copy(data, 14, ipBytes, 0, 4);
                var deviceIP = new IPAddress(ipBytes).ToString();

                // 서브넷 마스크 추출 (오프셋 18, 4바이트)
                var maskBytes = new byte[4];
                Array.Copy(data, 18, maskBytes, 0, 4);
                var subnetMask = new IPAddress(maskBytes).ToString();

                // 게이트웨이 추출 (오프셋 22, 4바이트)
                var gatewayBytes = new byte[4];
                Array.Copy(data, 22, gatewayBytes, 0, 4);
                var gateway = new IPAddress(gatewayBytes).ToString();

                // HTTP 포트 추출 (오프셋 116, 4바이트)
                var httpPort = BitConverter.ToInt32(data, 116);

                return new CameraInfo
                {
                    Ip = deviceIP,
                    Mac = string.Join("-", macBytes.Select(b => b.ToString("X2"))),
                    Type = httpPort != 0 ? "A" : "P", // HTTP 포트가 있으면 A타입, 없으면 P타입
                    Mask = subnetMask,
                    Gateway = gateway,
                    Version = "확인중..." // 비동기로 업데이트될 예정
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraService] 카메라 데이터 파싱 오류: {ex.Message}");
                return null;
            }
        }

        public async IAsyncEnumerable<CameraInfo> FindCamerasAsync([EnumeratorCancellation] CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<CameraInfo>();
            var foundCameras = new HashSet<string>();

            void Handler(CameraInfo camera)
            {
                if (foundCameras.Add(camera.Ip)) // 중복 방지
                {
                    channel.Writer.TryWrite(camera);
                }
            }

            CameraFound += Handler;

            try
            {
                await StartScanAsync();
                await Task.Delay(10000, token); // 10초 스캔
            }
            catch (TaskCanceledException)
            {
                // 정상적인 취소
            }
            finally
            {
                CameraFound -= Handler;
                await StopScanAsync();
                channel.Writer.Complete();
            }

            await foreach (var camera in channel.Reader.ReadAllAsync(token))
            {
                yield return camera;
            }
        }

        private async Task<string> GetCameraVersionAsync(string ip)
        {
            const int port = 7061;
            var buffer = new byte[4096];
            var stringBuilder = new StringBuilder();

            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.ReceiveTimeout = 3000;
                socket.SendTimeout = 3000;

                await socket.ConnectAsync(IPAddress.Parse(ip), port);

                await SendUserAuthAsync(socket);
                await Task.Delay(200);
                await SendCamVersionAsync(socket);

                int totalBytesRead = 0;
                int maxRetries = 5;
                int retryCount = 0;

                while (retryCount < maxRetries)
                {
                    try
                    {
                        int bytesRead = socket.Receive(buffer, totalBytesRead, buffer.Length - totalBytesRead, SocketFlags.None);
                        if (bytesRead == 0) break;

                        string part = Encoding.ASCII.GetString(buffer, totalBytesRead, bytesRead);
                        stringBuilder.Append(part);
                        totalBytesRead += bytesRead;

                        if (part.Contains("LOADVERSION="))
                            break;
                    }
                    catch (SocketException)
                    {
                        retryCount++;
                        await Task.Delay(100);
                    }
                }

                string message = stringBuilder.ToString();
                if (message.Contains("LOADVERSION="))
                {
                    var match = Regex.Match(message, @"LOADVERSION=([^;\r\n]+)");
                    if (match.Success)
                        return match.Groups[1].Value;
                }

                return "UNKNOWN";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CameraService] 버전 확인 오류 ({ip}): {ex.Message}");
                return "ERROR";
            }
        }

        private async Task SendUserAuthAsync(Socket socket)
        {
            string cmd = "CAM_USERAUTH;";
            string data = "USERLEVEL=vmY%39XEHjTMG828Lbqe2ocQ%3D%3D;";
            string checksum = CalculateChecksum(data);

            int authHeaderLength = 13 + cmd.Length;
            byte[] sendHeader = Encoding.ASCII.GetBytes(authHeaderLength.ToString("00") + data.Length.ToString().PadLeft(12, '0'));
            byte[] checksumByte = { Convert.ToByte(checksum, 16) };
            byte[] sendFooter = Encoding.ASCII.GetBytes(cmd + data);

            byte[] message = new byte[sendHeader.Length + checksumByte.Length + sendFooter.Length];
            sendHeader.CopyTo(message, 0);
            checksumByte.CopyTo(message, sendHeader.Length);
            sendFooter.CopyTo(message, sendHeader.Length + checksumByte.Length);

            await socket.SendAsync(message, SocketFlags.None);
        }

        private async Task SendCamVersionAsync(Socket socket)
        {
            string cmd = "CAMVERSION;";
            string data = "NOTHING;";
            string checksum = CalculateChecksum(data);
            string header = (13 + cmd.Length).ToString("00") + data.Length.ToString().PadLeft(12, '0');
            string message = header + Convert.ToChar(Convert.ToUInt32(checksum, 16)) + cmd + data;
            byte[] bytes = Encoding.ASCII.GetBytes(message);
            await socket.SendAsync(bytes, SocketFlags.None);
        }

        private static string CalculateChecksum(string dataToCalculate)
        {
            byte[] byteToCalculate = Encoding.ASCII.GetBytes(dataToCalculate);
            int checksum = byteToCalculate.Sum(b => b) & 0xff;
            return checksum.ToString("X2");
        }

        public void Dispose()
        {
            StopScanAsync().Wait(1000);
        }
    }
}