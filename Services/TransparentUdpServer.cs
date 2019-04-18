using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Org.Mentalis.Network.ProxySocket;
using SocksTun.Properties;

#if USEUDP

namespace SocksTun.Services
{
	class TransparentUdpServer : IService
	{
		private readonly DebugWriter debug;
		private readonly IDictionary<string, IService> services;
		private readonly UdpClient transparentSocksServer;

		private ConnectionTracker connectionTracker;
		public int Port { get; private set; }

        private TransparentUdpConnection connection;

        public TransparentUdpServer(DebugWriter debug, IDictionary<string, IService> services)
		{
			this.debug = debug;
			this.services = services;

			transparentSocksServer = new UdpClient(new IPEndPoint(IPAddress.Any, Settings.Default.SocksPort));
			transparentSocksServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
		}

		public void Start()
		{
			connectionTracker = (ConnectionTracker)services["connectionTracker"];

			Port = ((IPEndPoint) transparentSocksServer.Client.LocalEndPoint).Port;
			debug.Log(0, "TransparentUdpPort = " + Port);
            connection = new TransparentUdpConnection(transparentSocksServer, debug, connectionTracker, ConfigureSocksProxy);
            connection.Process();
        }

        public void Stop()
		{
            // TODO: This should close established connections
        }

		private static void ConfigureSocksProxy(ProxySocket proxySocket, IPEndPoint requestedEndPoint)
		{
			// TODO: Make this configurable
			proxySocket.ProxyType = ProxyTypes.Socks5Udp;
			proxySocket.ProxyEndPoint = new IPEndPoint(IPAddress.Loopback, 1080); //requestedEndPoint.Port == 443 ? 8000 : 1080);
		}
	}
}

#endif