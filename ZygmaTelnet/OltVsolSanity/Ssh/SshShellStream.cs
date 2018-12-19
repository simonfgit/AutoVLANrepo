using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Renci.SshNet;

namespace SwitchZygmaSetup.Ssh
{
    public class SshShellStream
    {
        private readonly Dictionary<string,int> _commands;
        private readonly ShellStream _shellStream;
        private readonly bool _debug;

        public SshShellStream(ShellStream shellStream, bool debug = false)
        {
            _shellStream = shellStream;
            _debug = debug;
            _commands = new Dictionary<string, int>();
        }

        public IDictionary<string,int> RunActions(string firstCommand, IEnumerable<SshShellAction> actions)
        {
            _commands.Clear();
            if (!string.IsNullOrEmpty(firstCommand))
            {
                InitCommand(firstCommand);
                _shellStream.WriteLine(firstCommand);
                IncrementCommand(firstCommand);
            }
            InternalRunActions(actions);
            return _commands;
        }

        private void InitCommand(string cmd)
        {
            if (!_commands.ContainsKey(cmd))
                _commands.Add(cmd, 0);
        }

        public IDictionary<string, int> RunActions(IEnumerable<SshShellAction> actions)
        {
            _commands.Clear();
            InternalRunActions(actions);
            return _commands;
        }

        private void InternalRunActions(IEnumerable<SshShellAction> actions)
        {
            foreach (var expectAction in actions)
                _shellStream.Expect(expectAction);
        }

        public bool RunAction(SshShellAction action)
        {
            _commands.Clear();
            _shellStream.Expect(action);
            return _commands.FirstOrDefault().Value > 0;
        }

        public SshShellAction WhenRegex(Regex regex, string command)
        {
            return new SshShellAction(regex, text =>
            {
                WriteCommand(command, text);
            });
        }

        public SshShellAction WhenText(string txt, string command)
        {
            return new SshShellAction(txt, text =>
            {
                WriteCommand(command, text);
            });
        }

        private void WriteCommand(string command, string text)
        {
            if (command == null)
                return;
            if (_debug)
                Console.WriteLine(text);
            InitCommand(command);
            _shellStream.WriteLine(command);
            IncrementCommand(command);
        }

        private void IncrementCommand(string command)
        {
            _commands[command] = _commands[command] + 1;
        }
    }
}
