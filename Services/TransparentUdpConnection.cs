using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Org.Mentalis.Network.ProxySocket;
using SocksTun.Properties;

#if USEUDP

namespace SocksTun.Services
{
    class TransparentUdpConnection
    {
        private readonly UdpClient server;
        private readonly DebugWriter debug;
        private readonly ConnectionTracker connectionTracker;
        private readonly ConfigureProxySocket configureProxySocket;
        private readonly IPEndPoint localEndPoint;
        private readonly ConcurrentDictionary<IPEndPoint, CountedProxySocket> proxies;

        public delegate void ConfigureProxySocket(ProxySocket proxySocket, IPEndPoint requestedEndPoint);
        private volatile bool running = false;

        class CountedProxySocket
        {
            public DateTime LastAccess { get; set; }
            private ProxySocket proxy;

            public ProxySocket Proxy
            {
                get
                {
                    LastAccess = DateTime.Now;
                    return proxy;
                }
                set
                {
                    LastAccess = DateTime.Now;
                    proxy = value;
                }
            }

            public TimeSpan Expired
            {
                get
                {
                    return DateTime.Now - LastAccess;
                }
            }

            public void Discard()
            {
                LastAccess = DateTime.MinValue;
            }
        }


		public TransparentUdpConnection(UdpClient server, DebugWriter debug, ConnectionTracker connectionTracker, ConfigureProxySocket configureProxySocket)
		{
            proxies = new ConcurrentDictionary<IPEndPoint, CountedProxySocket>();

            this.server = server;
            this.debug = debug;
			this.connectionTracker = connectionTracker;
			this.configureProxySocket = configureProxySocket;


            localEndPoint = (IPEndPoint)server.Client.LocalEndPoint;
            if (localEndPoint.Address.Equals(IPAddress.Any))
            {
                localEndPoint = new IPEndPoint(IPAddress.Parse(Settings.Default.IPAddress), localEndPoint.Port);
            }
        }

        public void Process()
		{
            running = true;
            server.BeginReceive(ReceiveCallback, null);

            SynchronizationContext.Current.Post( o =>
            {
                do
                {
                    proxies.Where(pair => pair.Value.Expired.TotalSeconds >= Settings.Default.UDPTimeoutSeconds).ToList().ForEach(pair =>
                    {
                        debug.Log(0, $"UDP disconnected: {pair.Key}");
                        proxies.TryRemove(pair.Key, out _);
                    });
                    Thread.Sleep(0);
                }
                while (running);
            }, null);
        }

        public void Stop()
        {
            running = false;
        }

        private bool trackConnection(Connection connection, IPEndPoint remoteEndPoint, out IPEndPoint targetEndPoint)
        {
            targetEndPoint = remoteEndPoint;
            if (connection != null)
            {
                var initialEndPoint = connection.Source;
                var requestedEndPoint = connection.Destination;
                var udpConnection = connectionTracker.GetUDPConnection(initialEndPoint, requestedEndPoint);

                var logMessage = string.Format("UDP {0}[{1}] {2} {{0}} connection to {3}",
                    udpConnection != null ? udpConnection.ProcessName : "unknown",
                    udpConnection != null ? udpConnection.PID : 0,
                    initialEndPoint, requestedEndPoint);

                debug.Log(1, logMessage);
                return true;
            }
            else
            {
                var logMessage = string.Format($"UDP remapping {remoteEndPoint} connection to {targetEndPoint}");
                debug.Log(1, logMessage);
                return true;
            }
        }

        private void UdpReceiveCallback(IAsyncResult ar)
        {
            var proxy = (CountedProxySocket)ar.AsyncState;
            var realProxy = proxy.Proxy;
            if (!realProxy.Connected)
            {
                proxy.Discard();
                return;
            }
            try
            {
                var remoteEndPoint = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
                var data = realProxy.UdpEndReceive(ar, ref remoteEndPoint);
                var connection = connectionTracker[new Connection(ProtocolType.Udp, realProxy.UdpEndPoint, remoteEndPoint)];
                if (trackConnection(connection, remoteEndPoint, out IPEndPoint targetEndPoint))
                {
                    server.Send(data, data.Length, connection.Destination);
                }

                realProxy.UdpBeginReceive(UdpReceiveCallback, proxy);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                debug.Log(0, ex.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
            var data = server.EndReceive(ar, ref remoteEndPoint);
            server.BeginReceive(ReceiveCallback, null);

            var dummyconn = new Connection(ProtocolType.Udp, localEndPoint, remoteEndPoint);
            var connection = connectionTracker[dummyconn]?.Mirror;
            if (trackConnection(connection, remoteEndPoint, out _))
            {
                if (!proxies.TryGetValue(connection.Source, out CountedProxySocket proxy))
                {
                    proxy = new CountedProxySocket()
                    {
                        Proxy = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    };

                    var realProxy = proxy.Proxy;
                    configureProxySocket(realProxy, null);
                    var ep = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
                    realProxy.Connect(ep);

                    connectionTracker[new Connection(ProtocolType.Udp, realProxy.UdpEndPoint, connection.Destination)] = dummyconn;

                    realProxy.UdpBeginReceive(UdpReceiveCallback, proxy);
                    proxies.TryAdd(connection.Source, proxy);
                }

                proxy.Proxy.UdpSend(data, connection.Destination);
            }
        }

    }
}

#endif