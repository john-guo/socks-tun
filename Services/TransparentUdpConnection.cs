using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Org.Mentalis.Network.ProxySocket;
using SocksTun.Properties;

#if false

namespace SocksTun.Services
{
	class TransparentUdpConnection
	{
		private readonly UdpClient client;
		private readonly DebugWriter debug;
		private readonly ConnectionTracker connectionTracker;
		private readonly ConfigureProxySocket configureProxySocket;

        Dictionary<Connection, ProxySocket> relays = new Dictionary<Connection, ProxySocket>();

		public delegate void ConfigureProxySocket(ProxySocket proxySocket, IPEndPoint requestedEndPoint);

		public TransparentUdpConnection(UdpClient client, DebugWriter debug, ConnectionTracker connectionTracker, ConfigureProxySocket configureProxySocket)
		{
			this.client = client;
			this.debug = debug;
			this.connectionTracker = connectionTracker;
			this.configureProxySocket = configureProxySocket;
		}

        class ProxyObject
        {
            public ProxySocket Proxy { get; set; }
            public Connection Connection { get; set; }

            public byte[] Buffer { get; set; }
        }

		public void Process()
		{
            var running = true;
            while (running)
            {

                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
                byte[] data = client.Receive(ref remoteEndPoint);

                var localEndPoint = (IPEndPoint)client.Client.LocalEndPoint;
                if (localEndPoint.Address.Equals(IPAddress.Any))
                {
                    localEndPoint = new IPEndPoint(IPAddress.Parse(Settings.Default.IPAddress), localEndPoint.Port);
                }
                var connection = connectionTracker[new Connection(ProtocolType.Udp, localEndPoint, remoteEndPoint)].Mirror;

                if (connection != null)
                {
                    var initialEndPoint = connection.Source;
                    var requestedEndPoint = connection.Destination;
                    var udpConnection = connectionTracker.GetUDPConnection(initialEndPoint, requestedEndPoint);

                    var logMessage = string.Format("{0}[{1}] {2} {{0}} connection to {3}",
                        udpConnection != null ? udpConnection.ProcessName : "unknown",
                        udpConnection != null ? udpConnection.PID : 0,
                        initialEndPoint, requestedEndPoint);
                    try
                    {
                        ProxySocket proxy;
                        if (!relays.ContainsKey(connection))
                        {
                            proxy = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            configureProxySocket(proxy, requestedEndPoint);
                            debug.Log(1, logMessage + " via {1}", "requested", proxy.ProxyEndPoint);

                            proxy.Connect(requestedEndPoint);

                            relays[connection] = proxy;
                            var buffer = new byte[10000];
                            proxy.BeginUdpReceive(buffer, 0, 10000, SocketFlags.None, ReceiveCallback, new ProxyObject() { Connection = connection, Proxy = proxy, Buffer = buffer });
                        }
                        else
                        {
                            proxy = relays[connection];
                        }

                        proxy.UdpSend(data, 0, data.Length, SocketFlags.None);

                        //proxy.Close();
                        //debug.Log(1, logMessage, "closing");
                    }
                    catch (Exception ex)
                    {
                        debug.Log(1, logMessage + ": {1}", "failed", ex.Message);
                    }

                    connectionTracker.QueueForCleanUp(connection);
                }
                else
                {
                    var udpConnection = connectionTracker.GetUDPConnection(remoteEndPoint, localEndPoint);
                    debug.Log(1, "{0}[{1}] {2} has no mapping",
                        udpConnection != null ? udpConnection.ProcessName : "unknown",
                        udpConnection != null ? udpConnection.PID : 0,
                        remoteEndPoint);
                    //client.Send(Encoding.ASCII.GetBytes("No mapping\r\n"));
                }
            }
			client.Close();
		}

        private void ReceiveCallback(IAsyncResult ar)
        {
            var obj = ar.AsyncState as ProxyObject;
            int size = obj.Proxy.EndUdpReceive(ar);
            byte[] sendBuf = new byte[size];
            System.Buffer.BlockCopy(obj.Buffer, 0, sendBuf, 0, size);

            obj.Proxy.BeginUdpReceive(obj.Buffer, 0, 10000, SocketFlags.None, ReceiveCallback, new ProxyObject() { Connection = obj.Connection, Proxy = obj.Proxy, Buffer = obj.Buffer });

            client.Send(sendBuf, size, obj.Connection.Source);
        }

    }
}

#endif