﻿using Eternet.Mikrotik;
using Eternet.Mikrotik.Entities.Ip;
using Eternet.Mikrotik.Entities.ReadWriters;
using Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Eternet.Mikrotik.Entities.Interface;
using Eternet.Mikrotik.Entities.Mpls;
using Eternet.Mikrotik.Entities.Routing.Ospf;
using Interface = Eternet.Mikrotik.Entities.Mpls.Ldp.Interface;
using Interfaces = Eternet.Mikrotik.Entities.Interface.Interfaces;

namespace Config.Vlan
{

    public enum VLanCreatedResult
    {
        BothVLanExists,
        BothVLanCreated,
        VLanToCcr2Created
    }

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
        
        #endregion

        public ConfigVlan(ILogger logger, ITikConnection connection)
        {
            _connection = connection;
            _logger = logger;
        }

        public Dictionary<string, (string iface, string mac)> GetListRoutersToConfig(IEntityReader<IpNeighbor> neighReader)
        {
            var result = new Dictionary<string, (string iface, string mac)>();

            var neighList = neighReader.GetAll().ToArray();

            foreach (var neigh in neighList)
            {
                if (!neigh.Interface.StartsWith("vlan") || !ValidIpAddress(neigh.Address4)) continue;

                result.Add(neigh.Address4, (neigh.Interface, neigh.MacAddress));
                _logger.Information(neigh.Address4);
            }

            return result;
        }

        public string GetUpLinkInterface(IEntityReader<IpNeighbor> neighReader, IEntityReader<Interfaces> ifaceReader)
        {
            var proxyNeigh = neighReader.GetAll().Where(n => n.Identity == "Saavedra_Proxy_ARP" || n.Identity == "Saavedra_CRF2");

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

        public VLanCreatedResult CreateVlanIfNotExists(string vlanName, string uplink, IEntityReadWriter<InterfaceVlan> vlanReadWriter)
        {
            var vlanId = vlanName.Replace("vlan", "");
            var vlanCrf2Name = "vlan2" + vlanId;
            var vlanCrf2Id = vlanCrf2Name.Replace("vlan", "");
            var vlanList = vlanReadWriter.GetAll().ToArray();

            if (Array.Exists(vlanList, n => n.Name == vlanCrf2Name))
                return VLanCreatedResult.BothVLanExists;

            var vlanCrf2 = new InterfaceVlan
            {
                Name = vlanCrf2Name,
                VlanId = Convert.ToInt32(vlanCrf2Id),
                Interface = uplink
            };

            if (!Array.Exists(vlanList, n => n.Name == vlanName))
            {
                var vlanCrf1 = new InterfaceVlan
                {
                    Name = vlanName,
                    VlanId = Convert.ToInt32(vlanId),
                    Interface = uplink
                };

                vlanReadWriter.Save(vlanCrf1);
                vlanReadWriter.Save(vlanCrf2);

                return VLanCreatedResult.BothVLanCreated;
            }

            vlanReadWriter.Save(vlanCrf2);
            return VLanCreatedResult.VLanToCcr2Created;
            
            //arrojar excepciones (equipo apagado por ejemplo) o bien hacer un try que devuelva un boolean
        }

        public string CheckFor260(string uplink, IEntityReader<IpNeighbor> neighReader)
        {
            var result = "No RB260";

            var uplinkNeighs = neighReader.GetAll().Where(n => n.Interface == uplink);

            foreach (var neigh in uplinkNeighs)
            {
                if (neigh.Board != "RB260GS") continue;

                result = neigh.Address4;
                break;
            }

            return result;
        }

        public Dictionary<string, string> GetVlanAddressList(IEntityReader<IpAddress> addressReader)
        {
            var result = new Dictionary<string, string>();

            var addressList = addressReader.GetAll().ToArray();

            foreach (var address in addressList)
            {
                var ip = address.Address.WhitOutNetwork();
                
                if (!address.Interface.StartsWith("vlan2") || !ValidIpAddress(ip)) continue;

                var nextIp = ip.GetNextIpAddress(1) + "/30";

                result.Add(address.Interface, nextIp);
            }

            return result;
        }

        //Este método hay que partirlo en cinco métodos, uno por cada Writer
        public void RoutingSetup(string vlanCrf1, string vlanCrf2, string ipAddress, VLanCreatedResult vlanStatus,
            IEntityWriter<IpAddress> addressWriter,
            IEntityWriter<Eternet.Mikrotik.Entities.Routing.Ospf.Interfaces> ospfIfaceWriter,
            IEntityWriter<Networks> ospfNetWriter, IEntityWriter<Interface> ldpIfaceWriter, 
            IEntityWriter<Eternet.Mikrotik.Entities.Mpls.Interface> mplsIfaceWriter)
        {
            var address = new IpAddress
            {
                Address = ipAddress,
                Interface = vlanCrf2
            };

            addressWriter.Save(address);

            var ospfIface = new Eternet.Mikrotik.Entities.Routing.Ospf.Interfaces
            {
                Interface = vlanCrf2,
                NetworkType = NetworkType.PointToPoint
            };

            ospfIfaceWriter.Save(ospfIface);

            var network = ipAddress.GetPreviousIpAddress(2) + "/30";

            var ospfNetwork = new Networks
            {
                Network = network,
                Area = "local2"
            };

            ospfNetWriter.Save(ospfNetwork);

            var ldpIface = new Interface
            {
                HelloInterval = "3s",
                HoldTime = "20s",
                Name = vlanCrf2
            };

            ldpIfaceWriter.Save(ldpIface);

            var mplsIface = new Eternet.Mikrotik.Entities.Mpls.Interface
            {
                Name = vlanCrf2,
                MplsMtu = 1516
            };

            mplsIfaceWriter.Save(mplsIface);

            if (vlanStatus != VLanCreatedResult.BothVLanCreated) return;

            _logger.Information("Comienza el cambio de puerto Access a Hybrid, presione Enter para continuar");
            Console.ReadLine();

            ospfIface.Interface = vlanCrf1;
            ospfIfaceWriter.Save(ospfIface);

            ldpIface.Name = vlanCrf1;
            ldpIfaceWriter.Save(ldpIface);

            mplsIface.Name = vlanCrf1;
            mplsIfaceWriter.Save(mplsIface);
        }
    }
}
