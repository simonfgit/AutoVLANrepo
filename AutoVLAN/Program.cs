using Eternet.Mikrotik;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;
using Log = Serilog.Log;
using Config.Vlan;
using Eternet.Mikrotik.Entities;
using Eternet.Mikrotik.Entities.Interface;
using Eternet.Mikrotik.Entities.Ip;

namespace AutoVLAN
{
    class Program
    {
        private static ITikConnection GetMikrotikConnection(string host, string user, string pass)
        {
            var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
            connection.Open(host, user, pass);
            return connection;
        }

        private static ConfigurationClass InitialSetup()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(AppContext.BaseDirectory))
                .AddJsonFile("appsettings.json", optional: false);

            var cfg = builder.Build();

            var mycfg = new ConfigurationClass();
            cfg.GetSection("ConfigurationClass").Bind(mycfg);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            return mycfg;
        }

        static void Main(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            // Set up configuration sources.
            var mycfg = InitialSetup();

            var connectionCrf = GetMikrotikConnection(mycfg.Host, mycfg.ApiUser, mycfg.ApiPass);

            var autoVlanCrf = new ConfigVlan(Log.Logger, connectionCrf);

            var neighReaderCrf = connectionCrf.CreateEntityReader<IpNeighbor>();

            var routerList = autoVlanCrf.GetListRoutersToConfig(neighReaderCrf);

            foreach (var router in routerList)
            {
                //tirar error de login?
                var connectionRouter = GetMikrotikConnection(router.Key, mycfg.ApiUser, mycfg.ApiPass);

                var autoVlanRouter = new ConfigVlan(Log.Logger, connectionRouter);

                var neighReaderRouter = connectionRouter.CreateEntityReader<IpNeighbor>();

                var ifaceReader = connectionRouter.CreateEntityReader<Interfaces>();

                var uplink = autoVlanRouter.GetUpLinkInterface(neighReaderRouter, ifaceReader);

                var vlanReadWriter = connectionRouter.CreateEntityReadWriter<InterfaceVlan>();

                var vlanCreation = autoVlanRouter.CreateVlanIfNotExists(router.Value, uplink, vlanReadWriter);

                connectionRouter.Dispose();
             }


            //foreach por cada elemento del diccionario que devolvio GetListRoutersToConfig y cambiar los parametros de la conexion en cada iteración?
            //en cada iteración creo una nueva conexion y la cierro al final

            connectionCrf.Dispose();
        }
    }
}
