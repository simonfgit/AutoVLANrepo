using System;
using System.Text.RegularExpressions;
using Renci.SshNet;

namespace SwitchZygmaSetup.Ssh
{
    public class SshShellAction : ExpectAction
    {
        public SshShellAction(Regex expect, Action<string> action) : base(expect, action)
        {
            
        }
        public SshShellAction(string expect, Action<string> action) : base(expect, action)
        {

        }
    }
}
