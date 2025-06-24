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
}
