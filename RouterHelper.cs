using Org.Mentalis.Network.ProxySocket;
using SocksTun.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace SocksTun
{
    public static class RouterHelper
    {
        private enum RouteOperation
        {
            add,
            change,
            delete,
        }
        private static bool RunRoute(string destAddr, string subMask, string gateway, int metric, int interfaceIndex, RouteOperation op)
        {
            var routerCmd = new Process
            {
                StartInfo = new ProcessStartInfo("route", $"{op} {destAddr} mask {subMask} {gateway} METRIC {metric} IF {interfaceIndex}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = true,
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
        }


        public static void SetupDefaultGateway(string name)
        {
            string externalIp = GetExternalIp();

            var link = NetworkInterface.GetAllNetworkInterfaces().Where(ni =>
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.OperationalStatus == OperationalStatus.Up &&
                ni.Name == name).First();

            var mask = link.GetIPProperties().UnicastAddresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork).First().IPv4Mask;
            var address = link.GetIPProperties().UnicastAddresses.Select(ip => ip.Address).Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).First();
            var gateway = link.GetIPProperties().GatewayAddresses.Select(ip => ip.Address).Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).First();
            var dns = link.GetIPProperties().DnsAddresses;

            var localDest = new IPAddress(mask.GetAddressBytes().Zip(address.GetAddressBytes(), (a, b) => (byte)(a & b)).ToArray());
            var ifidx = link.GetIPProperties().GetIPv4Properties().Index;

            var anyIp = IPAddress.Any.ToString();
            var noneIp = IPAddress.None.ToString();
            var gatewayStr = gateway.ToString();

            SetRoute(localDest.ToString(), mask.ToString(), gatewayStr, 1, ifidx);
            SetRoute(externalIp, noneIp, gatewayStr, 1, ifidx);
            SetRoute(anyIp, anyIp, gatewayStr, 99, ifidx, false);

            foreach (var d in dns)
            {
                SetRoute(d.ToString(), noneIp, gatewayStr, 1, ifidx);
            }

        }

        public static void SetupTapGateway(Guid guid, IPAddress specifiedGateway)
        {
            var link = NetworkInterface.GetAllNetworkInterfaces().Where(ni =>
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.OperationalStatus == OperationalStatus.Up &&
                new Guid(ni.Id) == guid).First();

            var mask = link.GetIPProperties().UnicastAddresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork).First().IPv4Mask;
            var address = link.GetIPProperties().UnicastAddresses.Select(ip => ip.Address).Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).First();
            var gateway = link.GetIPProperties().GatewayAddresses.Select(ip => ip.Address).Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).First();

            var localDest = new IPAddress(mask.GetAddressBytes().Zip(address.GetAddressBytes(), (a, b) => (byte)(a & b)).ToArray());
            var ifidx = link.GetIPProperties().GetIPv4Properties().Index;

            var anyIp = IPAddress.Any.ToString();
            var noneIp = IPAddress.None.ToString();
            var gatewayStr = specifiedGateway.ToString();

            DeleteRoute(anyIp, anyIp, gateway.ToString(), 1, ifidx);
            AddRoute(anyIp, anyIp, gatewayStr, 1, ifidx);

            SetRoute(localDest.ToString(), mask.ToString(), gatewayStr, 1, ifidx);
        }

        public static string GetExternalIp()
        {
            ProxySocket proxySocket = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            proxySocket.ProxyType = ProxyTypes.Socks5;
            proxySocket.ProxyEndPoint = new IPEndPoint(IPAddress.Loopback, 1080); //requestedEndPoint.Port == 443 ? 8000 : 1080);
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
    }
}
