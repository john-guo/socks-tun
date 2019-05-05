using IpHlpApidotnet;
using Org.Mentalis.Network.ProxySocket;
using SocksTun.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Reflection;

namespace SocksTun
{
    public static class NetworkHelper
    {
        public enum MetricIndex
        {
            DIRECT = 1,
            PROXY = 50,
            LAST = 999,
        }

        private enum RouteOperation
        {
            add,
            change,
            delete,
        }


        private static bool SetNetworkProfilePrivate(string adapterAlias)
        {
            try
            {
                var shell = $"Get-NetConnectionProfile -InterfaceAlias '{adapterAlias}' | Set-NetConnectionProfile -NetworkCategory Private";
                var cmd = new Process
                {
                    StartInfo = new ProcessStartInfo("powershell", $"-ExecutionPolicy Unrestricted -Command \"& {{{shell}}}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    }
                };

                return cmd.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            return false;
        }


        public static void SetFirewallRule()
        {
            var program = Assembly.GetExecutingAssembly().Location;
            var cmd = new Process
            {
                StartInfo = new ProcessStartInfo("netsh", $"advfirewall firewall add rule dir=in action=allow profile=any name=\"SocksTun\" program=\"{program}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                }
            };

            cmd.Start();
        }

        public static void RemoveFirewallRule()
        {
            var cmd = new Process
            {
                StartInfo = new ProcessStartInfo("netsh", $"advfirewall firewall delete rule name=\"SocksTun\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                }
            };

            cmd.Start();
        }

        private static bool RunRoute(string destAddr, string subMask, string gateway, int metric, int interfaceIndex, RouteOperation op)
        {
            var routerCmd = new Process
            {
                StartInfo = new ProcessStartInfo("route", $"{op} {destAddr} mask {subMask} {(string.IsNullOrWhiteSpace(gateway) ? string.Empty : gateway)}  {(metric == 0? string.Empty : $"METRIC {metric}")} {(interfaceIndex == 0 ? string.Empty : $"IF {interfaceIndex}")}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                }
            };

            return routerCmd.Start();
        }

        public static bool AddRoute(string destAddr, string subMask, string gateway, int metric, int interfaceIndex) =>
            RunRoute(destAddr, subMask, gateway, metric, interfaceIndex, RouteOperation.add);

        public static bool ChangeRoute(string destAddr, string subMask, string gateway, int metric, int interfaceIndex) =>
            RunRoute(destAddr, subMask, gateway, metric, interfaceIndex, RouteOperation.change);

        public static bool DeleteRoute(string destAddr, string subMask, string gateway, int metric, int interfaceIndex) =>
            RunRoute(destAddr, subMask, gateway, metric, interfaceIndex, RouteOperation.delete);

        public class RouteInfo {
            public string destination;
            public string mask;
            public int metric;
            public int interfaceIndex;
            public string nexthop;
            public string protocol;
            public string type;
            public string status;
        }

        private static List<RouteInfo> extraRoutes = new List<RouteInfo>();
        private static int defaultInterfaceIndex;
        private static string defaultGateway;
        private static string[] defaultDNS;

        public static List<RouteInfo> GetRouteTable()
        {
            List<RouteInfo> table = new List<RouteInfo>();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_IP4RouteTable");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                table.Add(
                    new RouteInfo
                    {
                        destination = queryObj["Destination"].ToString(),
                        mask = queryObj["Mask"].ToString(),
                        metric = (int)queryObj["Metric1"],
                        interfaceIndex = (int)queryObj["InterfaceIndex"],
                        nexthop = queryObj["NextHop"].ToString(),
                        protocol = queryObj["Protocol"].ToString(),
                        type = queryObj["Type"].ToString(),
                        status = queryObj["Status"]?.ToString() ?? string.Empty,
                    }
                );
            }

            return table;
        }

        public static bool SetRoute(string ipAddresses, string gateway, int metric, int interfaceIndex, bool needClean = true)
        {
            var slice = ipAddresses.Split('/');
            var address = slice[0];
            var num = int.Parse(slice[1]);
            var cap = 32 - num;

            var mask = num == 0 ? 0 : ~((uint)(1 << cap) - 1);
            mask = (uint)IPAddress.HostToNetworkOrder((int)mask);
            var maskaddr = new IPAddress(mask).ToString();

            return SetRoute(address, maskaddr, gateway, metric, interfaceIndex, needClean);
        }

        public static bool SetRoute(string destAddr, string subMask, string gateway, int metric, int interfaceIndex, bool needClean = true)
        {
            var table = GetRouteTable();
            var found = table.FirstOrDefault(t => t.destination == destAddr && t.mask == subMask && t.nexthop == gateway && t.interfaceIndex == interfaceIndex);
            if (found != null)
            {
                return ChangeRoute(destAddr, subMask, gateway, metric, interfaceIndex);
            }
            else
            {
                if (needClean)
                {
                    extraRoutes.Add(new RouteInfo
                    {
                        destination = destAddr,
                        mask = subMask,
                        nexthop = gateway,
                        metric = metric,
                        interfaceIndex = interfaceIndex
                    });
                }
                return AddRoute(destAddr, subMask, gateway, metric, interfaceIndex);
            }
        }

        public static void Cleanup()
        {
            foreach (var route in extraRoutes)
            {
                DeleteRoute(route.destination, route.mask, route.nexthop, route.metric, route.interfaceIndex);
            }

            if (defaultDNS != null)
            {
                SetDns(defaultInterfaceIndex, defaultDNS);
            }
        }

        private static NetworkInterface GetDefaultGatewayInterface()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => n.GetIPProperties()?.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork) == true)
                .FirstOrDefault();
        }

        private static NetworkInterface GetBestNetworkInterface(IPAddress destinationAddress)
        {
            uint destaddr = BitConverter.ToUInt32(destinationAddress.GetAddressBytes(), 0);

            int result = IPHlpAPI32Wrapper.GetBestInterface(destaddr, out uint interfaceIndex);
            if (result != 0)
                throw new Win32Exception(result);

            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.GetIPProperties().GetIPv4Properties()?.Index == interfaceIndex)
                .Where(ni => ni.GetIPProperties().GatewayAddresses.Any())
                .FirstOrDefault();
        }

        public static void SetupDefaultGateway()
        {
            string externalIp = GetExternalIp();

            var link = GetBestNetworkInterface(IPAddress.Parse(externalIp)) ?? GetDefaultGatewayInterface();
            if (link == null)
            {
                throw new Exception("No Gateway");
            }

            var mask = link.GetIPProperties().UnicastAddresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork).First().IPv4Mask;
            var address = link.GetIPProperties().UnicastAddresses.Select(ip => ip.Address).Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).First();
            var gateway = link.GetIPProperties().GatewayAddresses.Select(ip => ip.Address).Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).First();
            var dns = link.GetIPProperties().DnsAddresses;

            var localDest = new IPAddress(mask.GetAddressBytes().Zip(address.GetAddressBytes(), (a, b) => (byte)(a & b)).ToArray());
            var ifidx = link.GetIPProperties().GetIPv4Properties().Index;

            defaultInterfaceIndex = ifidx;

            var anyIp = IPAddress.Any.ToString();
            var noneIp = IPAddress.None.ToString();
            var gatewayStr = gateway.ToString();

            defaultGateway = gatewayStr;

            DeleteRoute(anyIp, anyIp, null, 0, 0);

            SetRoute(localDest.ToString(), mask.ToString(), gatewayStr, (int)MetricIndex.DIRECT, ifidx);
            SetRoute(externalIp, noneIp, gatewayStr, (int)MetricIndex.DIRECT, ifidx);
            SetRoute(anyIp, anyIp, gatewayStr, (int)MetricIndex.LAST, ifidx, false);

            if (string.IsNullOrWhiteSpace(Settings.Default.DNSServer))
            {
                //setup dns
                foreach (var d in dns)
                {
                    SetRoute(d.ToString(), noneIp, gatewayStr, (int)MetricIndex.DIRECT, ifidx);
                }
            }
            else
            {
                defaultDNS = GetDns(ifidx);
                SetDns(ifidx, new[] { Settings.Default.DNSServer });
            }

            //setup proxy
            var proxyAddr = IPAddress.Parse(Settings.Default.ProxyAddress);
            if (!IPAddress.IsLoopback(proxyAddr))
            {
                SetRoute(Settings.Default.ProxyAddress, noneIp, gatewayStr, (int)MetricIndex.DIRECT, ifidx);
            }
        }

        private static IPAddress GetSubAddress(IPAddress addr, IPAddress mask)
        {
            var addrbytes = addr.GetAddressBytes();
            var maskbytes = mask.GetAddressBytes();

            var subaddr = addrbytes.Aggregate(0, (a, b) => a << 8 | b) & maskbytes.Aggregate(0, (a, b) => a << 8 | b);
            return new IPAddress(IPAddress.HostToNetworkOrder(subaddr));
        }

        public static void SetupTapGateway(Guid guid)
        {
            var link = NetworkInterface.GetAllNetworkInterfaces().Where(ni =>
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.OperationalStatus == OperationalStatus.Up &&
                new Guid(ni.Id) == guid).First();

            var ifidx = link.GetIPProperties().GetIPv4Properties().Index;

            var anyIp = IPAddress.Any.ToString();
            var noneIp = IPAddress.None.ToString();
            var gatewayStr = anyIp;

            var localIP = IPAddress.Parse(Settings.Default.IPAddress);
            var mask = IPAddress.Parse(Settings.Default.SubnetMask);
            var subaddr = GetSubAddress(localIP, mask);

            SetRoute(
                subaddr.ToString(),
                Settings.Default.SubnetMask,
                gatewayStr, (int)MetricIndex.DIRECT, ifidx);

            if (string.IsNullOrWhiteSpace(Settings.Default.Rules))
            {
                AddRoute(anyIp, anyIp, gatewayStr, (int)MetricIndex.PROXY, ifidx);
            }
            else
            {
                var rules = File.ReadAllLines(Settings.Default.Rules);
                var count = 0;
                foreach (var line in rules)
                {
                    var rule = line.Trim();
                    if (string.IsNullOrWhiteSpace(rule))
                        continue;

                    var addr = rule;
                    var gateway = gatewayStr;
                    int metric = (int)MetricIndex.PROXY;
                    int idx = ifidx;

                    if (rule[0] == '#')
                        continue;
                    if (rule[0] == '-')
                    {
                        gateway = defaultGateway;
                        metric = (int)MetricIndex.DIRECT;
                        idx = defaultInterfaceIndex;
                        addr = rule.Substring(1);
                    }

                    count++;
                    SetRoute(addr, gateway, metric, idx);
                }

                if (count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(Settings.Default.DNSServer))
                    {
                        SetRoute(Settings.Default.DNSServer, IPAddress.None.ToString(), gatewayStr, (int)MetricIndex.PROXY, ifidx);
                    }
                }
                else
                {
                    AddRoute(anyIp, anyIp, gatewayStr, (int)MetricIndex.PROXY, ifidx);
                }
            }

            if (!SetNetworkProfilePrivate(link.Name))
            {
                throw new Exception("Cannot change to private profile");
            }
        }

        public static string GetExternalIp()
        {
            ProxySocket proxySocket = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            proxySocket.ProxyType = ProxyTypes.Socks5;
            proxySocket.ProxyEndPoint = new IPEndPoint(IPAddress.Parse(Settings.Default.ProxyAddress), Settings.Default.ProxyPort);
            proxySocket.Connect("api.ipify.org", 80);
            proxySocket.Send(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: api.ipify.org\r\n\r\n"));
            int recv = 0;
            byte[] buffer = new byte[1024];
            recv = proxySocket.Receive(buffer);
            StringBuilder sb = new StringBuilder();
            if (recv > 0)
            {
                sb.Append(Encoding.ASCII.GetString(buffer, 0, recv));
            }
            proxySocket.Close();

            string ip = string.Empty;
            using (var reader = new StringReader(sb.ToString()))
            {
                while (!string.IsNullOrEmpty(reader.ReadLine())) ;
                ip = reader.ReadLine();
            }
            return ip;
        }

        private static ManagementObject FindNetworkAdapterConfiguration(string id)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2",
                    $"SELECT * FROM Win32_NetworkAdapterConfiguration Where SettingID='{id}'");
            return searcher.Get().OfType<ManagementObject>().FirstOrDefault();
        }

        private static ManagementObject FindNetworkAdapterConfiguration(int interfaceIndex)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2",
                    $"SELECT * FROM Win32_NetworkAdapterConfiguration Where InterfaceIndex ={interfaceIndex}");
            return searcher.Get().OfType<ManagementObject>().FirstOrDefault();
        }

        private static string[] GetDns(int interfaceIndex)
        {
            var mo = FindNetworkAdapterConfiguration(interfaceIndex);
            return (string[])mo["DNSServerSearchOrder"];
        }
        private static void SetDns(int interfaceIndex, string[] dns)
        {
            var mo = FindNetworkAdapterConfiguration(interfaceIndex);

            var parameters = mo.GetMethodParameters("SetDNSServerSearchOrder");
            parameters["DNSServerSearchOrder"] = dns;
            mo.InvokeMethod("SetDNSServerSearchOrder", parameters, null);
        }

        public static void SetStaticIPAddress(string id, string ip, string mask, string dns)
        {
            var mo = FindNetworkAdapterConfiguration(id);

            if (!(bool)mo["IPEnabled"])
                return;

            var parameters = mo.GetMethodParameters("EnableStatic");
            parameters["IPAddress"] = new[] { ip };
            parameters["SubnetMask"] = new[] { mask };
            mo.InvokeMethod("EnableStatic", parameters, null);

            parameters = mo.GetMethodParameters("SetDNSServerSearchOrder");
            parameters["DNSServerSearchOrder"] = new[] { dns };
            mo.InvokeMethod("SetDNSServerSearchOrder", parameters, null);
        }
    }
}
