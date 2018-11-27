using Renci.SshNet;
using Renci.SshNet.Common;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SwitchZygmaSetup
{
    public class Shell
    {
        #region private_fields&consts

        private readonly string _host;
        private readonly string _user;
        private readonly string _pass;
        private readonly ILogger _log;
        private static readonly Dictionary<string, string> ZygmaAccessPorts = new Dictionary<string, string>
        {
            {"vlan24", "2"}, {"vlan26", "4"}, {"vlan27", "5"}, {"vlan41", "6"}, {"vlan21", "8"},
            {"vlan25", "9"}, {"vlan43", "10"}, {"vlan30", "11"}, {"vlan31", "12"},
            {"vlan34", "13"}, {"vlan28", "15"}, {"vlan35", "16"}, {"vlan19", "17"},
            {"vlan23", "18"}, {"vlan40", "20"}
        };

        #endregion

        public Shell(string host, string user, string pass, ILogger log)
        {
            _host = host;
            _user = user;
            _pass = pass;
            _log = log;
        }

        private bool GetExpect(ShellStream shell, string expect)
        {
            var exp = shell.Expect(expect, TimeSpan.FromSeconds(60));
            if (!string.IsNullOrEmpty(exp)) return true;
            LogTimeout(expect);
            return false;
        }

        private void LogTimeout(string expect)
        {
            _log.Error("Timeout! Esperando por: {expect}", expect);
        }

        private bool GetExpect(ShellStream shell, Regex expect)
        {
            var exp = shell.Expect(expect, TimeSpan.FromSeconds(60));
            if (string.IsNullOrEmpty(exp))
            {
                LogTimeout(exp);
                return false;
            }
            _log.Information(exp);
            return true;
        }

        public bool RunOnNeighbor(string ip, string user, string pass, string vlan)
        {
            IDictionary<TerminalModes, uint> termkvp = new Dictionary<TerminalModes, uint>();
            using (var client = new SshClient(_host, _user, _pass))
            {
                //await Task.Delay(1);
                client.Connect();
                termkvp.Add(TerminalModes.ECHO, 53);

                var mkprompt = new Regex(@"\[.*@.*\].>.$");
                var telnetprompt = new Regex(@"Switch(\(vlan\)){0,1}\#");
                var loginprompt = new Regex(@"Username:");
                var passprompt = new Regex(@"Password:");
                var port = ZygmaAccessPorts[vlan];
                var vlanId = vlan.Remove(0, 4);

                using (var shell = client.CreateShellStream("xterm", 160, 24, 800, 600, 1024, termkvp))
                {
                    if (!GetExpect(shell, mkprompt))
                    {
                        _log.Information("SSH {_host} tiempo de espera agotado esperando", _host);
                        return false;
                    }

                    var telnetCmd = $"/system telnet {ip}";

                    shell.WriteLine(telnetCmd);

                    _log.Information("Telnet a {ip}...", ip);

                    if (!GetExpect(shell, loginprompt))
                    {
                        _log.Error("No se puede conectar a {ip}", ip);
                        return false;
                    }

                    //user
                    shell.WriteLine(user);

                    if (!GetExpect(shell, passprompt))
                        return false;

                    //password
                    shell.WriteLine(pass);

                    if (!GetExpect(shell, telnetprompt))
                        return false;

                    _log.Information("Login OK en {ip}", ip);


                    shell.WriteLine("vlan");

                    if (!GetExpect(shell, telnetprompt))
                        return false;

                    shell.WriteLine("port-type " + port + " c-port");

                    if (!GetExpect(shell, telnetprompt))
                        return false;

                    shell.WriteLine("frame-type " + port + " tagged");

                    if (!GetExpect(shell, telnetprompt))
                        return false;

                    shell.WriteLine("egress-rule " + port + " trunk");

                    if (!GetExpect(shell, telnetprompt))
                        return false;

                    shell.WriteLine("pvid " + port + " " + vlanId );

                    if (!GetExpect(shell, telnetprompt))
                        return false;

                    shell.WriteLine("exit");
                    _log.Information("Desconectando de {ip}", ip);
                }
                //await Task.Delay(200);
                client.Disconnect();

            }
            return true;


        }


    }
}

