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
		private readonly UdpClient client;
		private readonly DebugWriter debug;
		private readonly ConnectionTracker connectionTracker;
		private readonly ConfigureProxySocket configureProxySocket;
        private readonly ProxySocket proxy;
        private readonly IPEndPoint localEndPoint;
        internal volatile bool running = true;
        private readonly ConcurrentDictionary<IPEndPoint, ConcurrentQueue<IPEndPoint>> connectionQueue;

        Dictionary<Connection, ProxySocket> relays = new Dictionary<Connection, ProxySocket>();

		public delegate void ConfigureProxySocket(ProxySocket proxySocket, IPEndPoint requestedEndPoint);

		public TransparentUdpConnection(UdpClient client, DebugWriter debug, ConnectionTracker connectionTracker, ConfigureProxySocket configureProxySocket)
		{
            connectionQueue = new ConcurrentDictionary<IPEndPoint, ConcurrentQueue<IPEndPoint>>();

            this.client = client;
			this.debug = debug;
			this.connectionTracker = connectionTracker;
			this.configureProxySocket = configureProxySocket;
            this.proxy = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            configureProxySocket(proxy, null);

            localEndPoint = (IPEndPoint)client.Client.LocalEndPoint;
            if (localEndPoint.Address.Equals(IPAddress.Any))
            {
                localEndPoint = new IPEndPoint(IPAddress.Parse(Settings.Default.IPAddress), localEndPoint.Port);
            }
        }

        public void Process()
		{
            var ep = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
            proxy.Connect(ep);

            client.BeginReceive(ReceiveCallback, null);
            proxy.UdpBeginReceive(UdpReceiveCallback, null);

            while (running)
            {
                Thread.Sleep(0);
            }
			client.Close();
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

                if (!connectionQueue.TryGetValue(requestedEndPoint, out ConcurrentQueue<IPEndPoint> queue))
                {
                    queue = new ConcurrentQueue<IPEndPoint>();
                    connectionQueue.TryAdd(requestedEndPoint, queue);
                }
                queue.Enqueue(remoteEndPoint);

                return true;
            }
            else
            {
                if (!connectionQueue.TryGetValue(remoteEndPoint, out ConcurrentQueue<IPEndPoint> queue))
                {

                    var udpConnection = connectionTracker.GetUDPConnection(remoteEndPoint, localEndPoint);
                    debug.Log(1, "UDP {0}[{1}] {2} has no mapping",
                        udpConnection != null ? udpConnection.ProcessName : "unknown",
                        udpConnection != null ? udpConnection.PID : 0,
                        remoteEndPoint);
                    return false;
                }
                else
                {
                    if (!queue.TryDequeue(out targetEndPoint))
                    {
                        var udpConnection = connectionTracker.GetUDPConnection(remoteEndPoint, localEndPoint);
                        debug.Log(1, "UDP {0}[{1}] {2} has no mapping",
                            udpConnection != null ? udpConnection.ProcessName : "unknown",
                            udpConnection != null ? udpConnection.PID : 0,
                            remoteEndPoint);
                        return false;
                    }

                    var logMessage = string.Format($"remapping {remoteEndPoint} connection to {targetEndPoint}");
                    debug.Log(1, logMessage);
                    return true;
                }

            }
        }

        private void UdpReceiveCallback(IAsyncResult ar)
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
            var data = proxy.UdpEndReceive(ar, ref remoteEndPoint);
            var connection = connectionTracker[new Connection(ProtocolType.Udp, localEndPoint, remoteEndPoint)]?.Mirror;
            if (trackConnection(connection, remoteEndPoint, out IPEndPoint targetEndPoint))
            {
                client.Send(data, data.Length, targetEndPoint);
                //connection = connectionTracker[new Connection(ProtocolType.Udp, localEndPoint, targetEndPoint)]?.Mirror;
                //if (connection != null)
                //{
                //    client.Send(data, data.Length, connection.Source);
                //}
            }

            proxy.UdpBeginReceive(UdpReceiveCallback, null);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
            var data = client.EndReceive(ar, ref remoteEndPoint);
            var connection = connectionTracker[new Connection(ProtocolType.Udp, localEndPoint, remoteEndPoint)]?.Mirror;
            if (trackConnection(connection, remoteEndPoint, out _))
            {
                proxy.UdpSend(data, connection.Destination);
            }

            client.BeginReceive(ReceiveCallback, null);
        }

    }
}

#endif