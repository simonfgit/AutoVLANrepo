using System.Net;

namespace SwitchZygmaSetup.Utils
{
    internal class Utils
    {
        public static string ConcatenatePwd(string deviceIp, string pwdPrefix)
        {
            var ipAddressBytes = (IPAddress.Parse(deviceIp).GetAddressBytes()[3]);
            return $"{pwdPrefix}{ipAddressBytes}";

        }

    }
}
