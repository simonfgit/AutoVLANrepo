using Eternet.Mikrotik;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using Log = Serilog.Log;
using Config.Vlan;
using Eternet.Mikrotik.Entities;
using Eternet.Mikrotik.Entities.Interface;
using Eternet.Mikrotik.Entities.Ip;
using Eternet.Mikrotik.Entities.Routing.Ospf;
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

            //Convertir este loop en un gran método - verificar bien todos los métodos involucrados
            foreach (var router in routerList)
            {
                var host = router.Key;

                var vlan = router.Value;

                //tirar error de login?

                var connectionRouter = GetMikrotikConnection(host, mycfg.ApiUser, mycfg.ApiPass);

                var autoVlanRouter = new ConfigVlan(Log.Logger, connectionRouter);

                var neighReaderRouter = connectionRouter.CreateEntityReader<IpNeighbor>();
                var ifaceReader = connectionRouter.CreateEntityReader<Interfaces>();

                var uplink = autoVlanRouter.GetUpLinkInterface(neighReaderRouter, ifaceReader);

                var vlanReadWriter = connectionRouter.CreateEntityReadWriter<InterfaceVlan>();

                var vlanCreation = autoVlanRouter.CreateVlanIfNotExists(vlan, uplink, vlanReadWriter);

                if (vlanCreation != "Both VLANs are already created")
                {
                    var rb260 = autoVlanRouter.CheckFor260(uplink, neighReaderRouter);

                    if (rb260 != "No RB260")
                    {
                        //logueo en un TXT los RB260 a los que hay que entrar para configurar manualmente
                        logFile.Information(vlan + " " + host);
                    }

                    //Armo una lista de todos los routers que no tienen ambas VLAN configuradas  
                    //para la proxima iteración
                    routerOspfList.Add((vlan, host, vlanCreation));
                }

                connectionRouter.Dispose();
            }

            //la proxima iteración (es decir la que sigue una vez confirmada la configuración de los 
            //rb260, va a trabajar con la lista de host+vlan+status y la lista de IP/30+VLAN del CRF2

            foreach (var router in routerOspfList)
            {
                var host = router.host;

                var vlan = router.vlan;

                var connectionRouter = GetMikrotikConnection(host, mycfg.ApiUser, mycfg.ApiPass);

                var addressWriter = connectionRouter.CreateEntityWriter<IpAddress>();
                var ospfIfaceWriter = connectionRouter.CreateEntityWriter<Eternet.Mikrotik.Entities.Routing.Ospf.Interfaces>();
                var ospfNetWriter = connectionRouter.CreateEntityWriter<Networks>();
                var ldpIfaceWriter = connectionRouter.CreateEntityWriter<Interface>();
                var mplsIfaceWriter = connectionRouter.CreateEntityWriter<Eternet.Mikrotik.Entities.Mpls.Interface>();

                var autoVlanRouter = new ConfigVlan(Log.Logger, connectionRouter);

                //esto se puede convertir en un método
                var vlanId = vlan.Remove(0, 4);
                var vlanCrf2 = "vlan2" + vlanId;
                var address = vlanAddressList[vlanCrf2];

                //este método al ser void no lo puedo testear - meterle excepciones para que sea testeable
                //viendolo de otra manera, no tiene ninguna lógica para testear...
                autoVlanRouter.RoutingSetup(vlan, address, addressWriter, ospfIfaceWriter,
                    ospfNetWriter, ldpIfaceWriter, mplsIfaceWriter);

                if (router.status == "CRF 1 and CRF2 VLANs were created")
                {
                    //tema... cualquier cambio sobre el OSPF me va a dejar sin gestión IP...
                    //dejar el cambio de la IP para el final, los otros no hacen perder la gestión
                    //para la interface ospf creo la vlan y luego elimino la ether

                    // estos son los puertos access, no olvidarse de configurar el Zygma
                }

                //otra posibilidad:
                //sin importar si ambas vlan estan creadas o no, agregar todos los campos menos la IP
                //ahora si dependiendo de que vlan esta creada, la IP se agrega o se modifica.
                //una vez listo esto se borran lo quedo en desuso

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
