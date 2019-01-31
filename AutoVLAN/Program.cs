using Eternet.Mikrotik;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Config.Vlan;
using Eternet.Mikrotik.Entities;
using Eternet.Mikrotik.Entities.Interface;
using Eternet.Mikrotik.Entities.Ip;
using Eternet.Mikrotik.Entities.Routing.Ospf;
using Eternet.Mikrotik.Entities.System;
using Serilog.Core;
using SwitchZygmaSetup;
using Interface = Eternet.Mikrotik.Entities.Mpls.Ldp.Interface;
using Interfaces = Eternet.Mikrotik.Entities.Interface.Interfaces;

// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo


namespace AutoVLAN
{
    internal class Program
    {
        private static IConfigurationRoot _configuration;
        private static Logger _logger;
        private static Logger _logFile;
        private static ConfigurationClass _mycfg;

        private static ITikConnection GetMikrotikConnection(string host, string user, string pass)
        {
            var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
            connection.Open(host, user, pass);
            return connection;
        }

        private static void InitialSetup()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(AppContext.BaseDirectory))
                .AddJsonFile("appsettings.json", optional: false);
            _configuration = builder.Build();

            _logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            _logFile = new LoggerConfiguration()
                .WriteTo.File(@"C:\Users\simon\source\repos\AutoVLANrepo\RB260.txt")
                .CreateLogger();
        }

        private static ConfigurationClass GetConfiguration()
        {
            var mycfg = new ConfigurationClass();
            _configuration.GetSection("ConfigurationClass").Bind(mycfg);
            return mycfg;
        }

        //Resolucion de dependencias de clase y metodo a utilizar
        private static Dictionary<string, string> GetVlanDictionary(string ip)
        {
            using (var connectionCrf2 = GetMikrotikConnection(ip, _mycfg.ApiUser, _mycfg.ApiPass))
            {
                var autoVlanCrf2 = new ConfigVlan(_logger, connectionCrf2);
                var addressReaderCrf2 = connectionCrf2.CreateEntityReader<IpAddress>();
                var vlanAddressList = autoVlanCrf2.GetVlanAddressList(addressReaderCrf2);
                return vlanAddressList;
            }
        }

        //Resolucion de dependencias de clase y metodo a utilizar
        private static Dictionary<string, (string iface, string mac)> GetNeigsRoutersDictionaryToConfig(string ip)
        {
            using (var connectionCrf = GetMikrotikConnection(ip, _mycfg.ApiUser, _mycfg.ApiPass))
            {
                var autoVlanCrf = new ConfigVlan(_logger, connectionCrf);
                var neighReaderCrf = connectionCrf.CreateEntityReader<IpNeighbor>();
                var routerList = autoVlanCrf.GetListRoutersToConfig(neighReaderCrf);
                return routerList;
            }
        }

        private static void Main(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            InitialSetup();

            // Set up configuration sources.
            _mycfg = GetConfiguration();

            var routerList = GetNeigsRoutersDictionaryToConfig(_mycfg.Host);
            var vlanAddressList = GetVlanDictionary("CCR2ip");


            var routerOspfList = GetRoutersOspfList(routerList);

            Process.Start(@"C:\Users\simon\source\repos\AutoVLANrepo\RB260.txt");

            _logger.Information("Presione Enter para confirmar que todos los RB260 han sido configurados");
            Console.ReadLine();

            //void??
            ConfigRouterOspf(routerOspfList, vlanAddressList, routerList);

            //TENER EN CUENTA EL SET UP DE VPLS EN EL CRF2, QUIZA CONVENGA HACER OTRA APLICACION

            //HABLAR CON NACHO RESPECTO DE LOS VPLS EN LOS ROUTERS (TENER EN CUENTA QUE NO SOLO
            //HAY VPLS EN LOS PRIMEROS SALTOS - EN UN CLASICO AP SE METE EN EL BRIDGE LA WLAN
            //Y EL VPLS, PARA QUE ANDE AUTOMATICAMENTE EL CRF2 TENDRIAN QUE ESTAR LOS DOS VPLS
            //EN EL MISMO BRIDGE JUNTO CON LA WLAN


            //Zygma
            //La IP para inicializar el Shell la saco del json (va a ser el Proxy ARP)
            //La IP del Zygma va a ser una constante.

            //Luego de la primer Iteracion la aplicación quedaria esperando confirmación
            //para continuar (obviamente se confirmaría luego de que todos los RB260 esten listos)


        }

        private static List<(string vlan, string host, string uplink, VLanCreatedResult status)> GetRoutersOspfList(Dictionary<string, (string iface, string mac)> routerList)
        {
            var routerOspfList = new List<(string vlan, string host, string uplink, VLanCreatedResult status)>();
            //Convertir este loop en un gran método - verificar bien todos los métodos involucrados
            foreach (var router in routerList)
            {
                var host = router.Key;
                var vlanCrf1 = router.Value.iface;
                var routerToSetup = GetMikrotikConnection(host, _mycfg.ApiUser, _mycfg.ApiPass);
                var autoVlanRouter = new ConfigVlan(_logger, routerToSetup);

                var neighReaderRouter = routerToSetup.CreateEntityReader<IpNeighbor>();
                var ifaceReader = routerToSetup.CreateEntityReader<Interfaces>();

                var uplink = autoVlanRouter.GetUpLinkInterface(neighReaderRouter, ifaceReader);
                var vlanReadWriter = routerToSetup.CreateEntityReadWriter<InterfaceVlan>();

                var vlanCreation = autoVlanRouter.CreateVlanIfNotExists(vlanCrf1, uplink, vlanReadWriter);
                if (vlanCreation != VLanCreatedResult.BothVLanCreated)
                {
                    var rb260 = autoVlanRouter.CheckFor260(uplink, neighReaderRouter);

                    if (rb260 != "No RB260")
                    {
                        //logueo en un TXT los RB260 a los que hay que entrar para configurar manualmente
                        _logFile.Information(vlanCrf1 + " " + rb260);
                    }
                    //Armo una lista de todos los routers que no tienen ambas VLAN configuradas  
                    routerOspfList.Add((vlanCrf1, host, uplink, vlanCreation));
                }
                routerToSetup.Dispose();
            }
            return routerOspfList;
        }

        private static void ConfigRouterOspf(List<(string vlan, string host, string uplink, VLanCreatedResult status)> routerOspfList, Dictionary<string,string> vlanAddressList, Dictionary<string, (string iface, string mac)> routerList)
        {
            foreach (var router in routerOspfList)
            {
                var connectionRouter = GetMikrotikConnection(router.host, _mycfg.ApiUser, _mycfg.ApiPass);

                var identityReader = connectionRouter.CreateEntityReader<Identity>();

                //var identity = identityReader.Get(i => i.Name != string.Empty).Name;
                var identity = identityReader.GetAll().FirstOrDefault()?.Name;

                _logger.Information("Comenzando con el setup de ruteo del equipo " + identity);

                var addressWriter = connectionRouter.CreateEntityWriter<IpAddress>();
                var ospfIfaceWriter = connectionRouter.CreateEntityWriter<Eternet.Mikrotik.Entities.Routing.Ospf.Interfaces>();
                var ospfNetWriter = connectionRouter.CreateEntityWriter<Networks>();
                var ldpIfaceWriter = connectionRouter.CreateEntityWriter<Interface>();
                var mplsIfaceWriter = connectionRouter.CreateEntityWriter<Eternet.Mikrotik.Entities.Mpls.Interface>();

                var autoVlanRouter = new ConfigVlan(_logger, connectionRouter);

                //esto se puede convertir en un método
                var vlanId = router.vlan.Replace("vlan", "");
                var vlanCrf2 = "vlan2" + vlanId;
                var address = vlanAddressList[vlanCrf2];

                //este método al ser void no lo puedo testear - meterle excepciones para que sea testeable
                //viendolo de otra manera, hay un solo If para testear...
                autoVlanRouter.RoutingSetup(router.vlan, vlanCrf2, address, router.status, addressWriter, ospfIfaceWriter,
                    ospfNetWriter, ldpIfaceWriter, mplsIfaceWriter);

                if (router.status == VLanCreatedResult.BothVLanCreated)
                {
                    var mac = routerList[router.host].mac;

                    var shell = new Shell(_mycfg.Host, _mycfg.ApiUser, _mycfg.ApiPass, _logger);

                    var cmd = "/ip address set [find where interface=" + router.uplink + "] interface=" + router.vlan;

                    shell.RunOnNeighbor2(mac, identity, _mycfg.ApiUser, _mycfg.ApiPass, cmd);

                    shell.RunOnNeighbor("192.168.1.1", "admin", "", router.vlan);

                    _logger.Information("El cambio de puerto Access a Trunk ha sido realizado, presione Enter para continuar");
                    Console.ReadLine();

                    //no olvidar de borrar lo que no se usa mas, ya que las interfaces mpls y ospf las
                    //voy a crear nuevas, no voy a editar las existentes, siempre hablando de los
                    //puertos access. Quizá convenga hacerlo a mano...
                }

                connectionRouter.Dispose();
            }
        }
    }
}
