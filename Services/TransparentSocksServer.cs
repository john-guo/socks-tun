using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Org.Mentalis.Network.ProxySocket;
using SocksTun.Properties;

namespace SocksTun.Services
{
	class TransparentSocksServer : IService
	{
		private readonly DebugWriter debug;
		private readonly IDictionary<string, IService> services;
		private readonly TcpListener transparentSocksServer;

		private ConnectionTracker connectionTracker;
		public int Port { get; private set; }
        public IPAddress Address { get; private set; }


		public TransparentSocksServer(DebugWriter debug, IDictionary<string, IService> services)
		{
			this.debug = debug;
			this.services = services;

			transparentSocksServer = new TcpListener(IPAddress.Any, Settings.Default.SocksPort);
			transparentSocksServer.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
		}

		public void Start()
		{
			connectionTracker = (ConnectionTracker)services["connectionTracker"];

			transparentSocksServer.Start();
			Port = ((IPEndPoint) transparentSocksServer.LocalEndpoint).Port;
            Address = IPAddress.Parse(Settings.Default.IPAddress);

            debug.Log(0, $"TransparentSocks = {Address}:{Port}");
			transparentSocksServer.BeginAcceptSocket(NewTransparentSocksConnection, null);
		}

		public void Stop()
		{
            // TODO: This should close established connections
            transparentSocksServer.Stop();
        }

		private void NewTransparentSocksConnection(IAsyncResult ar)
		{
			Socket client = null;
			try
			{
				client = transparentSocksServer.EndAcceptSocket(ar);
            }
            catch (SystemException)
			{
			}

            try
            {
                transparentSocksServer.BeginAcceptSocket(NewTransparentSocksConnection, null);
            }
            catch { }

            if (client == null) return;
			var connection = new TransparentSocksConnection(client, debug, connectionTracker, ConfigureSocksProxy);
			connection.Process();
		}

		private static void ConfigureSocksProxy(ProxySocket proxySocket, IPEndPoint requestedEndPoint)
		{
			// TODO: Make this configurable
			proxySocket.ProxyType = ProxyTypes.Socks5;
            var address = IPAddress.Parse(Settings.Default.ProxyAddress);
			proxySocket.ProxyEndPoint = new IPEndPoint(address, Settings.Default.ProxyPort);
		}
	}
}
