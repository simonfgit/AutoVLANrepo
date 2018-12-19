using Eternet.Mikrotik;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Log = Serilog.Log;
using Config.Vlan;
using Eternet.Mikrotik.Entities;
using Eternet.Mikrotik.Entities.Interface;
using Eternet.Mikrotik.Entities.Ip;
using Eternet.Mikrotik.Entities.Routing.Ospf;
using Eternet.Mikrotik.Entities.System;
using SwitchZygmaSetup;
using Interface = Eternet.Mikrotik.Entities.Mpls.Ldp.Interface;
using Interfaces = Eternet.Mikrotik.Entities.Interface.Interfaces;

namespace AutoVLAN
{
    internal class Program
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

        private static void Main(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            // Set up configuration sources.
            var mycfg = InitialSetup();

            var logFile = new LoggerConfiguration()
                .WriteTo.File(@"C:\Users\simon\source\repos\AutoVLANrepo\RB260.txt")
                .CreateLogger();

            //CRF

            var connectionCrf = GetMikrotikConnection(mycfg.Host, mycfg.ApiUser, mycfg.ApiPass);
            var autoVlanCrf = new ConfigVlan(Log.Logger, connectionCrf);
            var neighReaderCrf = connectionCrf.CreateEntityReader<IpNeighbor>();
            var routerList = autoVlanCrf.GetListRoutersToConfig(neighReaderCrf);

            //CRF 2  

            var connectionCrf2 = GetMikrotikConnection("IPCRF2", mycfg.ApiUser, mycfg.ApiPass);
            var autoVlanCrf2 = new ConfigVlan(Log.Logger, connectionCrf2);
            var addressReaderCrf2 = connectionCrf2.CreateEntityReader<IpAddress>();
            var vlanAddressList = autoVlanCrf2.GetVlanAddressList(addressReaderCrf2);

            var routerOspfList = new List<(string vlan, string host, string status)>();
            
            //doy de alta la variable para poder utilizarla en la proxima iteración pero en realidad
            //tendría que ser un parámetro de salida del método
            var uplink = "";
            //Convertir este loop en un gran método - verificar bien todos los métodos involucrados
            foreach (var router in routerList)
            {
                var host = router.Key;

                var vlanCrf1 = router.Value.iface;

                //tirar error de login?

                var connectionRouter = GetMikrotikConnection(host, mycfg.ApiUser, mycfg.ApiPass);

                var autoVlanRouter = new ConfigVlan(Log.Logger, connectionRouter);

                var neighReaderRouter = connectionRouter.CreateEntityReader<IpNeighbor>();
                var ifaceReader = connectionRouter.CreateEntityReader<Interfaces>();

                uplink = autoVlanRouter.GetUpLinkInterface(neighReaderRouter, ifaceReader);

                var vlanReadWriter = connectionRouter.CreateEntityReadWriter<InterfaceVlan>();

                var vlanCreation = autoVlanRouter.CreateVlanIfNotExists(vlanCrf1, uplink, vlanReadWriter);

                if (vlanCreation != "Both VLANs are already created")
                {
                    var rb260 = autoVlanRouter.CheckFor260(uplink, neighReaderRouter);

                    if (rb260 != "No RB260")
                    {
                        //logueo en un TXT los RB260 a los que hay que entrar para configurar manualmente
                        logFile.Information(vlanCrf1 + " " + host);
                    }

                    //Armo una lista de todos los routers que no tienen ambas VLAN configuradas  
                    //para la proxima iteración
                    routerOspfList.Add((vlanCrf1, host, vlanCreation));
                }

                connectionRouter.Dispose();
            }

            //la proxima iteración (es decir la que sigue una vez confirmada la configuración de los 
            //rb260, va a trabajar con la lista de host+vlan+status y la lista de IP/30+VLAN del CRF2
            //para los casos de los puertos Access, la aplicación va a devolver el control entre 
            //router y router por si algo saliese mal y hubiese que hacer rollback

            foreach (var router in routerOspfList)
            {
                var host = router.host;

                var vlanCrf1 = router.vlan;

                var vlanStatus = router.status;

                var connectionRouter = GetMikrotikConnection(host, mycfg.ApiUser, mycfg.ApiPass);

                var addressWriter = connectionRouter.CreateEntityWriter<IpAddress>();
                var ospfIfaceWriter = connectionRouter.CreateEntityWriter<Eternet.Mikrotik.Entities.Routing.Ospf.Interfaces>();
                var ospfNetWriter = connectionRouter.CreateEntityWriter<Networks>();
                var ldpIfaceWriter = connectionRouter.CreateEntityWriter<Interface>();
                var mplsIfaceWriter = connectionRouter.CreateEntityWriter<Eternet.Mikrotik.Entities.Mpls.Interface>();

                var autoVlanRouter = new ConfigVlan(Log.Logger, connectionRouter);

                //esto se puede convertir en un método
                var vlanId = vlanCrf1.Remove(0, 4);
                var vlanCrf2 = "vlan2" + vlanId;
                var address = vlanAddressList[vlanCrf2];

                //este método al ser void no lo puedo testear - meterle excepciones para que sea testeable
                //viendolo de otra manera, hay un solo If para testear...
                autoVlanRouter.RoutingSetup(vlanCrf1, vlanCrf2, address, vlanStatus, addressWriter, ospfIfaceWriter,
                    ospfNetWriter, ldpIfaceWriter, mplsIfaceWriter);
                
                //aca vendría el cambio en la interface de la IP de Uplink por mac telnet y el setup
                //del Zygma y luego devuelvo el control para corroborar

                if (router.status == "CRF 1 and CRF2 VLANs were created")
                {
                    var mac = routerList[host].mac;

                    var shell = new Shell(mycfg.Host, mycfg.ApiUser, mycfg.ApiPass, Log.Logger);

                    var cmd = "/ip address set [find where interface=" + uplink + "] interface=" + vlanCrf1;

                    var identityReader = connectionRouter.CreateEntityReader<Identity>();

                    //var identity = identityReader.Get(i => i.Name != string.Empty).Name;
                    var identity = identityReader.GetAll().FirstOrDefault()?.Name;

                    shell.RunOnNeighbor2(mac, identity, mycfg.ApiUser, mycfg.ApiPass, cmd);

                    shell.RunOnNeighbor("192.168.1.1", "admin", "", vlanCrf1);

                    Log.Logger.Information("El cambio de puerto Access a Trunk ha sido realizado, presione Enter para continuar");
                    Console.ReadLine();

                    //no olvidar de borrar lo que no se usa mas, ya que las interfaces mpls y ospf las
                    //voy a crear nuevas, no voy a editar las existentes, siempre hablando de los
                    //puertos access. Quizá convenga hacerlo a mano...
                }

                connectionRouter.Dispose();
            }



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






            //connectionCrf.Dispose();
        }
    }
}
