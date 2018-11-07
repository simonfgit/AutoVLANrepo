using System;
using System.Net;

namespace Extensions
{
    public static class IpAddressExtensions
    {
        private const string Missmatchlen = "Lengths of IP address and subnet mask do not match.";

        public static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask)
        {
            var ipAdressBytes = address.GetAddressBytes();
            var subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException(Missmatchlen);

            var broadcastAddress = new byte[ipAdressBytes.Length];
            for (var i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }

        public static IPAddress GetNetworkAddress(this IPAddress address, IPAddress subnetMask)
        {
            var ipAdressBytes = address.GetAddressBytes();
            var subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException(Missmatchlen);

            var broadcastAddress = new byte[ipAdressBytes.Length];
            for (var i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] & (subnetMaskBytes[i]));
            }
            return new IPAddress(broadcastAddress);
        }

        public static bool IsInSameSubnet(this IPAddress address2, IPAddress address, IPAddress subnetMask)
        {
            var network1 = address.GetNetworkAddress(subnetMask);
            var network2 = address2.GetNetworkAddress(subnetMask);
            return network1.Equals(network2);
        }

        public static int ToInteger(this IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            var result = bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3];
            return result;
        }

        public static int Compare(this IPAddress ip1, IPAddress ip2)
        {
            var int1 = ip1.ToInteger();
            var int2 = ip2.ToInteger();
            return ((int1 - int2) >> 0x1F) | (int)((uint)-(int1 - int2) >> 0x1F);
        }

        public static bool LessOrEqualTo(this IPAddress ip1, IPAddress ip2)
        {
            return ip1.Compare(ip2) <= 0;
        }

        public static bool GreaterOrEqualTo(this IPAddress ip1, IPAddress ip2)
        {
            return ip1.Compare(ip2) >= 0;
        }
    }
}
