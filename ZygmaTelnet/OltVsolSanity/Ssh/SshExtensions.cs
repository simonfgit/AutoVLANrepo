using System.Text.RegularExpressions;

namespace SwitchZygmaSetup.Ssh
{
    public static class SshExtensions
    {
        public static SshShellAction NewAction(this SshShellStream shellStream, Regex expect, string command)
        {
            return shellStream.WhenRegex(expect, command);
        }

        public static SshShellAction NewAction(this SshShellStream shellStream, string expect, string command)
        {
            return shellStream.WhenText(expect, command);
        }

    }
}