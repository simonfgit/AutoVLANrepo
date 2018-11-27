using Serilog;
using SwitchZygmaSetup;
using Xunit;

namespace SwitchZygmaIntegrationTest
{
    public class SwitchZygmaIntegrationTest
    {
        [Fact]
        public void TelnetSwitchTest()
        {
            var log = new LoggerConfiguration()
                .WriteTo.Console()
                //.WriteTo.file("log.txt")
                .CreateLogger();
            var shell = new Shell("192.168.1.1", "admin", "", log); //Mikrotik desde donde se ejecuta el telnet
            const string cmd = "save start";
            Assert.True(shell.RunOnNeighbor("192.168.1.2", "admin", "admin", cmd));//datos del switch
        }

    }
}
