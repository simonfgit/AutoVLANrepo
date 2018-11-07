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
    }
}
