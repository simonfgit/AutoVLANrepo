using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SwitchZygmaSetup.Ssh;

namespace SwitchZygmaSetup
{
    public class MacTelnet
    {
        private readonly SshShellStream _shellStream;

        private readonly string _mac;
        private bool _login;
        private readonly string _identity;

        public MacTelnet(SshShellStream shellStream, string mac, string identity)
        {
            _shellStream = shellStream;
            _mac = mac;
            _identity = identity;
            _login = false;
        }

        public bool Login(string user = "admin", string pass = "")
        {
            var firstprompt = new Regex(@"\[.*@.*\].>.$");
            var mkpromptstr = @"\[.*@{identity}\].>.*$".Replace("{identity}", _identity);
            var secondprompt = new Regex(mkpromptstr);
            var actions = new List<SshShellAction>
            {
                _shellStream.NewAction(firstprompt, $"/tool mac-telnet {_mac}"),
                _shellStream.NewAction("Login:", user),
                _shellStream.NewAction("Password:", pass),
                _shellStream.NewAction(secondprompt, Environment.NewLine)
            };
            var results = _shellStream.RunActions(actions);
            _login = results.All(kv => kv.Value > 0);
            return _login;
        }

        public bool Run(string firstCommand, IEnumerable<SshShellAction> actions)
        {
            var results = _shellStream.RunActions(firstCommand, actions);
            return results.All(kv => kv.Value > 0);
        }

    }

}
