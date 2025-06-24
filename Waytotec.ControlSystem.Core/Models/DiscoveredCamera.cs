// Waytotec.ControlSystem.Core/Models/DiscoveredCamera.cs
using System.ComponentModel;
using System.Net;

namespace Waytotec.ControlSystem.Core.Models
{
    /// <summary>
    /// 검색된 카메라 정보 클래스
    /// </summary>
    public class DiscoveredCamera : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private IPAddress _ipAddress = IPAddress.None;
        private string _macAddress = string.Empty;
        private string _serialNumber = string.Empty;
        private string _version = string.Empty;
        private CameraStatus _status = CameraStatus.Unknown;
        private DateTime _lastSeen = DateTime.Now;

        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                }
            }
        }

        public IPAddress IpAddress
        {
            get => _ipAddress;
            set
            {
                if (!_ipAddress.Equals(value))
                {
                    _ipAddress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IpAddressString));
                }
            }
        }

        public string MacAddress
        {
            get => _macAddress;
            set
            {
                if (_macAddress != value)
                {
                    _macAddress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedMacAddress));
                }
            }
        }

        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (_serialNumber != value)
                {
                    _serialNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                if (_version != value)
                {
                    _version = value;
                    OnPropertyChanged();
                }
            }
        }

        public CameraStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public DateTime LastSeen
        {
            get => _lastSeen;
            set
            {
                if (_lastSeen != value)
                {
                    _lastSeen = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LastSeenText));
                }
            }
        }

        // 네트워크 정보
        public IPAddress SubnetMask { get; set; } = IPAddress.None;
        public IPAddress Gateway { get; set; } = IPAddress.None;

        // 포트 정보
        public int HttpPort { get; set; } = 80;
        public int RtspPort { get; set; } = 554;
        public int HttpJpegPort { get; set; } = 8080;
        public int PtzPort { get; set; } = 0;

        // UI 표시용 속성들
        public string IpAddressString => IpAddress?.ToString() ?? "알 수 없음";

        public string FormattedMacAddress => FormatMacAddress(MacAddress);

        public string StatusText => GetStatusText();

        public string StatusColor => GetStatusColor();

        public string LastSeenText => GetLastSeenText();

        public string NetworkInfo => $"{IpAddressString} / {SubnetMask} / {Gateway}";

        public string PortInfo => $"HTTP:{HttpPort}, RTSP:{RtspPort}, JPEG:{HttpJpegPort}";

        private string FormatMacAddress(string mac)
        {
            if (string.IsNullOrEmpty(mac))
                return "알 수 없음";

            if (mac.Length == 12)
            {
                return string.Join(":",
                    Enumerable.Range(0, 6)
                    .Select(i => mac.Substring(i * 2, 2)));
            }

            return mac;
        }

        private string GetStatusText()
        {
            return Status switch
            {
                CameraStatus.Online => "정상",
                CameraStatus.Offline => "오프라인",
                CameraStatus.Error => "오류",
                CameraStatus.Updating => "업데이트중",
                CameraStatus.Connecting => "연결중",
                _ => "알 수 없음"
            };
        }

        private string GetStatusColor()
        {
            return Status switch
            {
                CameraStatus.Online => "Green",
                CameraStatus.Offline => "Gray",
                CameraStatus.Error => "Red",
                CameraStatus.Updating => "Orange",
                CameraStatus.Connecting => "Blue",
                _ => "Black"
            };
        }

        private string GetLastSeenText()
        {
            var timeSpan = DateTime.Now - LastSeen;

            if (timeSpan.TotalMinutes < 1)
                return "방금 전";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}분 전";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}시간 전";

            return LastSeen.ToString("MM/dd HH:mm");
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{IpAddressString} ({FormattedMacAddress}) - {StatusText}";
        }

        public override bool Equals(object? obj)
        {
            if (obj is DiscoveredCamera other)
            {
                return IpAddress.Equals(other.IpAddress);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return IpAddress.GetHashCode();
        }
    }

    /// <summary>
    /// 카메라 상태 열거형
    /// </summary>
    public enum CameraStatus
    {
        Unknown,
        Online,
        Offline,
        Error,
        Updating,
        Connecting
    }
}