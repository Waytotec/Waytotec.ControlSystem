using System.Text.RegularExpressions;

namespace Waytotec.ControlSystem.Core.Models
{
    public class MacAddress : IEquatable<MacAddress>
    {
        public byte[] AddressBytes { get; }

        public MacAddress(string mac)
        {
            AddressBytes = Parse(mac);
        }

        public override string ToString()
        {
            return string.Join("-", AddressBytes.Select(b => b.ToString("X2")));
        }

        public static byte[] Parse(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac))
                throw new ArgumentException("MAC address is null or empty.");

            mac = mac.Replace(":", "-").ToUpperInvariant();
            var parts = mac.Split('-');

            if (parts.Length != 6 || parts.Any(p => p.Length != 2 || !Regex.IsMatch(p, "^[0-9A-F]{2}$")))
                throw new FormatException("Invalid MAC address format.");

            return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MacAddress);
        }

        public bool Equals(MacAddress? other)
        {
            if (other == null) return false;
            return AddressBytes.SequenceEqual(other.AddressBytes);
        }

        public override int GetHashCode()
        {
            return BitConverter.ToInt32(AddressBytes, 0);
        }
    }
}
