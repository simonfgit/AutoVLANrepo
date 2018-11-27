using Eternet.Mikrotik;
using Eternet.Mikrotik.Entities.Ip;
using Eternet.Mikrotik.Entities.ReadWriters;
using Moq;
using Serilog;
using System.Collections.Generic;
using Eternet.Mikrotik.Entities.Interface;
using Xunit;
using System;

namespace Config.Vlan.Unit.Tests
{
    public class ConfigVlanUnitTests
    {

        #region private_fields&consts

        private static List<IpNeighbor> GetFakeProxyNeighList()
        {
            return new List<IpNeighbor>
            {
                new IpNeighbor
                {
                    Address4 = "192.168.0.7",
                    Interface = "vlan70"
                },
                new IpNeighbor
                {
                    Address4 = "10.0.5.87",
                    Interface = "br5"
                },
                new IpNeighbor
                {
                    Address4 = "10.0.8.90",
                    Interface = "vlan40"
                },
                new IpNeighbor
                {
                    Address4 = "192.168.0.5",
                    Interface = "ether3"
                },
                new IpNeighbor
                {
                    Address4 = "",
                    Interface = "vlan30"
                },
                new IpNeighbor
                {
                    Address4 = "",
                    Interface = "ether7"
                },
                new IpNeighbor
                {
                    Address4 = "10.0.9.58",
                    Interface = "vlan78"
                }
            };
        }

        private static Dictionary<string, string> GetFakeListRoutersToConfig()
        {
            var dic = new Dictionary<string, string>
            {
                {"10.0.8.90", "vlan40"},
                {"10.0.9.58", "vlan78"}
            };

            return dic;
        }

        private static List<IpNeighbor> GetFakeRouterNeighList1()
        {
            return new List<IpNeighbor>
            {
                new IpNeighbor
                {
                    Interface = "vpls_Saavedra",
                    Identity = "Saavedra_Proxy_ARP",
                    Board = "CCR1072-1G-8S+",
                    Address4 = "10.0.5.137"
                },
                new IpNeighbor
                {
                    Interface = "ether1",
                    Identity = "Saavedra_Proxy_ARP",
                    Board = "CCR1072-1G-8S+",
                    Address4 = "10.0.5.137"
                },
                new IpNeighbor
                {
                    Interface = "ether3",
                    Identity = "Moreno 56",
                    Board = "RB260GS",
                    Address4 = "10.150.14.80"
                },
                new IpNeighbor
                {
                    Interface = "ether1",
                    Identity = "Saavedra_CRF2",
                    Board = "CCR1072-1G-8S+",
                    Address4 = "10.0.6.245"
                }
            };
        }

        private static List<IpNeighbor> GetFakeRouterNeighList2()
        {
            return new List<IpNeighbor>
            {
                new IpNeighbor
                {
                    Interface = "ether1",
                    Identity = "RB260 to Saavedra",
                    Board = "RB260GS",
                    Address4 = "10.150.8.47"
                },
                new IpNeighbor
                {
                    Interface = "ether2",
                    Identity = "Vieytes 92",
                    Board = "CCR1072-1G-8S+",
                    Address4 = "10.0.5.47"
                }
            };
        }

        private static List<Interfaces> GetFakeRouterIfacesList()
        {
            return new List<Interfaces>
            {
                new Interfaces
                {
                    DefaultName = "vpls_Saavedra",
                    Type = "VPLS"
                },
                new Interfaces
                {
                    DefaultName = "ether1",
                    Type = "Ethernet"
                },
                new Interfaces
                {
                    DefaultName = "ether3",
                    Type = "Ethernet"
                }
            };
        }

        private List<InterfaceVlan> GetFakeVlanList1()
        {
            return new List<InterfaceVlan>
            {
                new InterfaceVlan
                {
                    Name = "vlan56"
                }
            };
        }

        private List<InterfaceVlan> GetFakeVlanList2()
        {
            return new List<InterfaceVlan>
            {
                new InterfaceVlan
                {
                    Name = "vlan56"
                },
                new InterfaceVlan
                {
                    Name = "vlan256"
                }
            };
        }

        private List<InterfaceVlan> GetFakeVlanList3()
        {
            return new List<InterfaceVlan>();
        }

        private readonly Mock<IEntityReader<IpNeighbor>> _neighReader;

        private readonly Mock<ITikConnection> _connection;

        private readonly Mock<ILogger> _logger;

        #endregion

        public ConfigVlanUnitTests()
        {
            _connection = new Mock<ITikConnection>();

            _neighReader = new Mock<IEntityReader<IpNeighbor>>();

            _logger = new Mock<ILogger>();
        }

        [Fact]
        public void ExpectedRouterList()
        {
            var neighList = GetFakeProxyNeighList();
            _neighReader.Setup(r => r.GetAll()).Returns(neighList.ToArray);

            var fakeListRoutersToConfig = GetFakeListRoutersToConfig();

            var configVlan = new ConfigVlan(_logger.Object, _connection.Object);

            var listRoutersToConfig = configVlan.GetListRoutersToConfig(_neighReader.Object);

            Assert.Equal(fakeListRoutersToConfig, listRoutersToConfig);
        }

        [Fact]
        public void ExpectedUplinkInterface()
        {
            var neighList = GetFakeRouterNeighList1();
            _neighReader.Setup(r => r.GetAll()).Returns(neighList.ToArray);

            var ifaceList = GetFakeRouterIfacesList();
            var ifaceReader = new Mock<IEntityReader<Interfaces>>();
            ifaceReader.Setup(r => r.GetAll()).Returns(ifaceList.ToArray);

            var configVlan = new ConfigVlan(_logger.Object, _connection.Object);

            var upLinkIface = configVlan.GetUpLinkInterface(_neighReader.Object, ifaceReader.Object);

            Assert.Equal("ether1", upLinkIface);
        }
        
        [Fact]
        public void VlanShouldBeCreated()
        {
            var vlanList = GetFakeVlanList1();
            var vlanReadWriter1 = new Mock<IEntityReadWriter<InterfaceVlan>>();
            vlanReadWriter1.Setup(r => r.GetAll()).Returns(vlanList.ToArray);

            vlanList = GetFakeVlanList2();
            var vlanReadWriter2 = new Mock<IEntityReadWriter<InterfaceVlan>>();
            vlanReadWriter2.Setup(r => r.GetAll()).Returns(vlanList.ToArray);

            vlanList = GetFakeVlanList3();
            var vlanReadWriter3 = new Mock<IEntityReadWriter<InterfaceVlan>>();
            vlanReadWriter3.Setup(r => r.GetAll()).Returns(vlanList.ToArray);
            
            var configVlan = new ConfigVlan(_logger.Object, _connection.Object);

            var result1 = configVlan.CreateVlanIfNotExists("vlan56", "ether1", vlanReadWriter1.Object);
            var result2 = configVlan.CreateVlanIfNotExists("vlan56", "ether1", vlanReadWriter2.Object);
            var result3 = configVlan.CreateVlanIfNotExists("vlan56", "ether1", vlanReadWriter3.Object);

            Assert.Equal("CRF2 VLAN was created", result1);
            Assert.Equal("Both VLANs are already created", result2);
            Assert.Equal("Access Port", result3);
        }

        [Fact]
        public void Rb260ShouldBeChecked()
        {
            var neighList1 = GetFakeRouterNeighList1();
            _neighReader.Setup(r => r.GetAll()).Returns(neighList1.ToArray);

            var neighList2 = GetFakeRouterNeighList2();
            var neighReader2 = new Mock<IEntityReader<IpNeighbor>>();
            neighReader2.Setup(r => r.GetAll()).Returns(neighList2.ToArray);

            var configVlan = new ConfigVlan(_logger.Object, _connection.Object);

            var result1 = configVlan.CheckFor260("ether1", _neighReader.Object);
            var result2 = configVlan.CheckFor260("ether1", neighReader2.Object);

            Assert.Equal("No RB260", result1);
            Assert.Equal("10.150.8.47", result2);
        }
    }
}
