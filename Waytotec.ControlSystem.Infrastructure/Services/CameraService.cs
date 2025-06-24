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
    public class CameraService : ICameraService, ICameraDiscoveryService
    {
        private readonly CameraDiscoveryService _discoveryService;
        public CameraService()
        {
            _discoveryService = new CameraDiscoveryService();
        }

        public async IAsyncEnumerable<CameraInfo> FindCamerasAsync([EnumeratorCancellation] CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<CameraInfo>();

            void Handler(FindCamera._waytocam wt, string ip)
            {
                var camIp = new IPAddress(wt.ipAddress).ToString();

                Task.Run(async () =>
                {
                    var version = await GetCameraVersionAsync(camIp);

                    var info = new CameraInfo
                    {
                        Ip = camIp,
                        Mac = string.Join("-", wt.mac.Select(b => b.ToString("X2"))),
                        Type = wt.httpjpegport != 0 ? "A" : "P",
                        Mask = new IPAddress(wt.ipMask).ToString(),
                        Gateway = new IPAddress(wt.ipGateway).ToString(),
                        Version = version
                    };

                    channel.Writer.TryWrite(info);
                });

                Debug.WriteLine($"[INFO] Found camera: {camIp}");
            }

            FindCamera.CallSendMsg += Handler;
            var finder = new FindCamera();
            finder.StartBind();
            finder.send_command();

            try
            {
                await Task.Delay(10000, token);
            }
            catch (TaskCanceledException) { }

            finder.Stop();
            FindCamera.CallSendMsg -= Handler;

            while (channel.Reader.TryRead(out var item))
                yield return item;
        }

        private async Task<string> GetCameraVersionAsync(string ip)
        {
            const int port = 7061;
            var buffer = new byte[4096];
            var stringBuilder = new StringBuilder();

            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(IPAddress.Parse(ip), port);

                await SendUserAuthAsync(socket);
                await Task.Delay(100);
                await SendCamVersionAsync(socket);

                socket.ReceiveTimeout = 2000;
                int bytesRead;

                do
                {
                    try
                    {
                        bytesRead = socket.Receive(buffer);
                        if (bytesRead == 0) break;

                        string part = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        stringBuilder.Append(part);

                        if (part.Contains("LOADVERSION="))
                            break;
                    }
                    catch (SocketException ex)
                    {
                        Debug.WriteLine($"[ERROR] Receive failed: {ex.Message}");
                        break;
                    }
                } while (true);

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
                Debug.WriteLine($"GetCameraVersionAsync error: {ex.Message}");
                return "ERROR";
            }
        }

        private async Task SendUserAuthAsync(Socket socket)
        {
            string cmd = "CAM_USERAUTH;";
            string data = "USERLEVEL=vmY%39XEHjTMG828Lbqe2ocQ%3D%3D;";
            string checksum = CalculateChecksum(data);

            int authHeaderLength = 13 + cmd.Length;
            int authDataLength = data.Length;

            byte[] sendHeader = Encoding.ASCII.GetBytes(authHeaderLength.ToString("00") + authDataLength.ToString().PadLeft(12, '0'));
            byte[] byteCheckSum = Encoding.Unicode.GetBytes(Convert.ToChar(Convert.ToUInt32(checksum, 16)).ToString());
            byte[] sendFooter = Encoding.ASCII.GetBytes(cmd + data);
            Array.Resize(ref byteCheckSum, 1);
            byte[] newByte = new byte[sendHeader.Length + sendFooter.Length + 1];
            sendHeader.CopyTo(newByte, 0);
            byteCheckSum.CopyTo(newByte, sendHeader.Length);
            sendFooter.CopyTo(newByte, sendHeader.Length + byteCheckSum.Length);


            //string header = (13 + cmd.Length).ToString("00") + data.Length.ToString().PadLeft(12, '0');
            //string message = header + Convert.ToChar(Convert.ToUInt32(checksum, 16)) + cmd + data;
            //byte[] bytes = Encoding.ASCII.GetBytes(message);
            //await socket.SendAsync(bytes, SocketFlags.None);

            //var buffer = new byte[4096];
            BeginSend(socket, newByte);


        }

        public void BeginSend(Socket socket, byte[] message)
        {
            try
            {
                socket.BeginSend(message, 0, message.Length, SocketFlags.None, new AsyncCallback(SendCallBack), message);
            }
            catch (SocketException e)
            {

            }
        }

        private void SendCallBack(IAsyncResult IAR)
        {
            string message = "";
            if (IAR.AsyncState.GetType() == typeof(byte[]))
            {
                message = Encoding.ASCII.GetString((byte[])IAR.AsyncState);
            }
            else if (IAR.AsyncState.GetType() == typeof(string))
            {
                message = (string)IAR.AsyncState;
            }

            //txtSocketLog.InvokeIfNeed(() => txtSocketLog.Text += (Environment.NewLine + String.Format("전송 완료 : {0}", message)));
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

        private string CalculateChecksum(string dataToCalculate)
        {
            byte[] byteToCalculate = Encoding.ASCII.GetBytes(dataToCalculate);
            int checksum = 0;

            foreach (byte chData in byteToCalculate)
            {
                checksum += chData;
            }

            checksum &= 0xff;
            return checksum.ToString("X2");
        }

        // ICameraDiscoveryService 구현 (새로운 기능)
        public bool IsDiscovering => _discoveryService.IsDiscovering;

        public event EventHandler<CameraDiscoveredEventArgs>? CameraDiscovered
        {
            add => _discoveryService.CameraDiscovered += value;
            remove => _discoveryService.CameraDiscovered -= value;
        }

        public event EventHandler<DiscoveryProgressEventArgs>? DiscoveryProgress
        {
            add => _discoveryService.DiscoveryProgress += value;
            remove => _discoveryService.DiscoveryProgress -= value;
        }

        public event EventHandler<EventArgs>? DiscoveryCompleted
        {
            add => _discoveryService.DiscoveryCompleted += value;
            remove => _discoveryService.DiscoveryCompleted -= value;
        }

        public async Task<IEnumerable<DiscoveredCamera>> DiscoverCamerasAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return await _discoveryService.DiscoverCamerasAsync(timeout, cancellationToken);
        }

        public async Task StartContinuousDiscoveryAsync(CancellationToken cancellationToken = default)
        {
            await _discoveryService.StartContinuousDiscoveryAsync(cancellationToken);
        }

        public async Task StopDiscoveryAsync()
        {
            await _discoveryService.StopDiscoveryAsync();
        }

        public async Task<IEnumerable<DiscoveredCamera>> DiscoverCamerasInRangeAsync(IPAddress startIp, IPAddress endIp, CancellationToken cancellationToken = default)
        {
            return await _discoveryService.DiscoverCamerasInRangeAsync(startIp, endIp, cancellationToken);
        }

        public async Task<DiscoveredCamera?> VerifyCameraAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
        {
            return await _discoveryService.VerifyCameraAsync(ipAddress, cancellationToken);
        }

        public void Dispose()
        {
            _discoveryService?.Dispose();
        }
    }
}