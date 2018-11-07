using Eternet.Mikrotik;
using Eternet.Mikrotik.Entities.Ip;
using Eternet.Mikrotik.Entities.ReadWriters;
using Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Eternet.Mikrotik.Entities.Interface;

namespace Config.Vlan
{
    public class ConfigVlan
    {
        #region private_fields&consts

        private readonly ITikConnection _connection;
        private readonly ILogger _logger;

        private static bool ValidIpAddress(string address)
        {
            var result = false;

            var subnetMask = "255.255.255.0";

            if (!string.IsNullOrEmpty(address))
                if (address.IsAddressOnSubnet("10.0.5.0", subnetMask) ||
                    address.IsAddressOnSubnet("10.0.6.0", subnetMask) ||
                    address.IsAddressOnSubnet("10.0.7.0", subnetMask) ||
                    address.IsAddressOnSubnet("10.0.8.0", subnetMask) ||
                    address.IsAddressOnSubnet("10.0.9.0", subnetMask) ||
                    address.IsAddressOnSubnet("10.0.10.0", subnetMask) ||
                    address.IsAddressOnSubnet("10.0.11.0", subnetMask) ||
                    address.IsAddressOnSubnet("10.33.0.0", subnetMask))
                {
                    result = true;
                }

            return result;
        }

        //private static bool ValidIPAddress(string address)
        //{
        //    var result = false;

        //    if (address != null)
        //        if (address.StartsWith("10.0.5.") ||
        //            address.StartsWith("10.0.6.") ||
        //            address.StartsWith("10.0.7.") ||
        //            address.StartsWith("10.0.8.") ||
        //            address.StartsWith("10.0.9.") ||
        //            address.StartsWith("10.0.10.") ||
        //            address.StartsWith("10.0.11.") ||
        //            address.StartsWith("10.33.0."))
        //        {
        //            result = true;
        //        }

        //    return result;
        //}

        #endregion

        public ConfigVlan(ILogger logger, ITikConnection connection)
        {
            _connection = connection;
            _logger = logger;
        }

        public Dictionary<string, string> GetListRoutersToConfig(IEntityReader<IpNeighbor> neighReader)
        {
            var result = new Dictionary<string, string>();

            var neighList = neighReader.GetAll().ToArray();

            foreach (var neigh in neighList)
            {
                if (!neigh.Interface.StartsWith("vlan") || !ValidIpAddress(neigh.Address4)) continue;

                result.Add(neigh.Address4, neigh.Interface);
                _logger.Information(neigh.Address4);
            }

            return result;
        }

        public string GetUpLinkInterface(IEntityReader<IpNeighbor> neighReader, IEntityReader<Interfaces> ifaceReader)
        {
            var proxyNeigh = neighReader.GetAll().Where(n => n.Identity == "Saavedra_Proxy_ARP");

            var neighIfaces = proxyNeigh.Select(n => n.Interface).ToArray();

            var ifaces = ifaceReader.GetAll().Where(i => i.Type == "Ethernet").ToArray();

            var result = "";

            foreach (var neighIface in neighIfaces)
            {
                if (ifaces.FirstOrDefault(i => i.DefaultName == neighIface) == null) continue;

                result = neighIface;
                break;
            }
            
            return result;
        }

        public string CreateVlanIfNotExists(string vlanName, string uplink, IEntityReadWriter<InterfaceVlan> vlanReadWriter)
        {
            var vlanId = vlanName.Remove(0, 4);
            var vlanCrf2Name = "vlan2" + vlanId;
            var vlanCrf2Id = vlanCrf2Name.Remove(0, 4);
            var vlanList = vlanReadWriter.GetAll().ToArray();

            if (!Array.Exists(vlanList, n => n.Name == vlanName)) return "Access Port";

            if (Array.Exists(vlanList, n => n.Name == vlanCrf2Name))
                    return "Both VLANs are already created";

            var vlanCrf2 = new InterfaceVlan
            {
                Name = vlanCrf2Name,
                VlanId = Convert.ToInt32(vlanCrf2Id),
                Interface = uplink
            };

            vlanReadWriter.Save(vlanCrf2);
            return "CRF2 VLAN was created";
            

            //arrojar excepciones (equipo apagado por ejemplo) o bien hacer un try que devuelva un boolean

            //si el puerto es access llamare al metodo que hace el laburo y una vez terminado
            //tengo que verificar si hay un RB260 antes del router
        }
    }
}
