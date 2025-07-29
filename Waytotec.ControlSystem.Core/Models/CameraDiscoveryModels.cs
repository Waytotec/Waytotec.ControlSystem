using System.Net;
using System.Runtime.InteropServices;


namespace Waytotec.ControlSystem.Core.Models
{
    /// <summary>
    /// 카메라 검색 응답 구조체 (C 구조체 CFG_NVRAM_STRUCT 변환)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CameraNvRamStruct
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] MacAddress;                   // MAC Address

        public uint IpAddress;                      // IP Address
        public uint IpMask;                         // Subnet Mask  
        public uint IpGateway;                      // Gateway

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] TargetName;                   // Target Name (Serial String)

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] TargetUserName;               // Target User Name

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] TargetPassword;               // Target Password

        public uint Signature;                      // Signature (CFGS)

        public uint UseWlan;                        // WLAN 사용 여부

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] Ssid;                         // SSID

        public uint AuthType;                       // 인증 타입
        public uint KeyType;                        // 키 타입

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] Key;                          // Key

        public uint HttpPort;                       // HTTP Port
        public uint RtspPort;                       // RTSP Port  
        public uint HttpJpegPort;                   // HTTP JPEG Port
        public uint PtzPort;                        // PTZ Port
    }

    /// <summary>
    /// 카메라 검색 프로토콜 구조체
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CameraDiscoveryMessage
    {
        public uint Message;                        // 메시지 타입 (GCFG, GCOK 등)
        public uint Version;                        // 버전
        public CameraNvRamStruct ConfigData;        // 설정 데이터
    }

    public class CameraDiscoveryModels
    {
        public string Id { get; set; } = string.Empty;
        public IPAddress IpAddress { get; set; } = IPAddress.None;
        public string MacAddress { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public CameraStatus Status { get; set; } = CameraStatus.Unknown;
        public DateTime LastSeen { get; set; } = DateTime.Now;

        // 네트워크 정보
        public IPAddress SubnetMask { get; set; } = IPAddress.None;
        public IPAddress Gateway { get; set; } = IPAddress.None;

        // 포트 정보
        public int HttpPort { get; set; } = 80;
        public int RtspPort { get; set; } = 554;
        public int HttpJpegPort { get; set; } = 8080;
        public int PtzPort { get; set; } = 0;

        // UI 표시용 속성들
        public string IpAddressString => IpAddress.ToString();
        public string FormattedMacAddress => FormatMacAddress(MacAddress);
        public string StatusText => GetStatusText();


        private string FormatMacAddress(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac.Length != 12)
                return mac;

            return string.Join(":",
                Enumerable.Range(0, 6)
                .Select(i => mac.Substring(i * 2, 2)));
        }

        private string GetStatusText()
        {
            return Status switch
            {
                CameraStatus.Online => "정상",
                CameraStatus.Offline => "오프라인",
                CameraStatus.Error => "오류",
                CameraStatus.Updating => "업데이트중",
                _ => "알 수 없음"
            };
        }
    }


    /// <summary>
    /// 카메라 검색 이벤트 인자
    /// </summary>
    public class CameraDiscoveredEventArgs : EventArgs
    {
        public DiscoveredCamera Camera { get; }
        public IPEndPoint Source { get; }

        public CameraDiscoveredEventArgs(DiscoveredCamera camera, IPEndPoint source)
        {
            Camera = camera;
            Source = source;
        }
    }

    /// <summary>
    /// 검색 진행률 이벤트 인수
    /// </summary>
    public class DiscoveryProgressEventArgs : EventArgs
    {
        public TimeSpan ElapsedTime { get; }
        public int DiscoveredCount { get; }

        public DiscoveryProgressEventArgs(TimeSpan elapsedTime, int discoveredCount = 0)
        {
            ElapsedTime = elapsedTime;
            DiscoveredCount = discoveredCount;
        }
    }
}
