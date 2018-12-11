using System;
using System.Linq;
using System.Net;

namespace Extensions
{
    public static class StringExtensions
    {
        public static bool IsAddressOnSubnet(this string saddress, string ssubnet, string smask)
        {
            var address = IPAddress.Parse(saddress);
            var subnet = IPAddress.Parse(ssubnet);
            var mask = IPAddress.Parse(smask);
            return address.IsInSameSubnet(subnet, mask);
        }

        public static string WhitOutNetwork(this string address)
        {
            return address.Split('/').FirstOrDefault();
        }

        public static string GetNextIpAddress(this string ipAddress, uint increment)
        {
            var addressBytes = IPAddress.Parse(ipAddress).GetAddressBytes().Reverse().ToArray();
            var ipAsUint = BitConverter.ToUInt32(addressBytes, 0);
            var nextAddress = BitConverter.GetBytes(ipAsUint + increment);
            return string.Join(".", nextAddress.Reverse());
        }

        public static string GetPreviousIpAddress(this string ipAddress, uint decrement)
        {
            var addressBytes = IPAddress.Parse(ipAddress).GetAddressBytes().Reverse().ToArray();
            var ipAsUint = BitConverter.ToUInt32(addressBytes, 0);
            var nextAddress = BitConverter.GetBytes(ipAsUint - decrement);
            return string.Join(".", nextAddress.Reverse());
        }
    }
}
