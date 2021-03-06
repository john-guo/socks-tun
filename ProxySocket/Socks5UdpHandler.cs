/*
    Copyright ?2002, The KPD-Team
    All rights reserved.
    http://www.mentalis.org/

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    - Redistributions of source code must retain the above copyright
       notice, this list of conditions and the following disclaimer. 

    - Neither the name of the KPD-Team, nor the names of its contributors
       may be used to endorse or promote products derived from this
       software without specific prior written permission. 

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL
  THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
  (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
  SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
  HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
  STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
  OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Org.Mentalis.Network.ProxySocket.Authentication;
using SocksTun.Properties;

#if USEUDP

namespace Org.Mentalis.Network.ProxySocket {
	/// <summary>
	/// Implements the SOCKS5 protocol.
	/// </summary>
	internal sealed class Socks5UdpHandler : SocksHandler {

        UdpClient udpClient;
        int localPort;

		/// <summary>
		/// Initiliazes a new Socks5Handler instance.
		/// </summary>
		/// <param name="server">The socket connection with the proxy server.</param>
		/// <exception cref="ArgumentNullException"><c>server</c>  is null.</exception>
		public Socks5UdpHandler(Socket server) : this(server, "") {}
		/// <summary>
		/// Initiliazes a new Socks5Handler instance.
		/// </summary>
		/// <param name="server">The socket connection with the proxy server.</param>
		/// <param name="user">The username to use.</param>
		/// <exception cref="ArgumentNullException"><c>server</c> -or- <c>user</c> is null.</exception>
		public Socks5UdpHandler(Socket server, string user) : this(server, user, "") {}
		/// <summary>
		/// Initiliazes a new Socks5Handler instance.
		/// </summary>
		/// <param name="server">The socket connection with the proxy server.</param>
		/// <param name="user">The username to use.</param>
		/// <param name="pass">The password to use.</param>
		/// <exception cref="ArgumentNullException"><c>server</c> -or- <c>user</c> -or- <c>pass</c> is null.</exception>
		public Socks5UdpHandler(Socket server, string user, string pass) : base(server, user) {
			Password = pass;
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            localPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
        }
        /// <summary>
        /// Starts the synchronous authentication process.
        /// </summary>
        /// <exception cref="ProxyException">Authentication with the proxy server failed.</exception>
        /// <exception cref="ProtocolViolationException">The proxy server uses an invalid protocol.</exception>
        /// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
        /// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
        private void Authenticate() {
			Server.Send(new byte [] {5, 2, 0, 2});
			byte[] buffer = ReadBytes(2);
			if (buffer[1] == 255)
				throw new ProxyException("No authentication method accepted.");
			AuthMethod authenticate;
			switch (buffer[1]) {
				case 0:
					authenticate = new AuthNone(Server);
					break;
				case 2:
					authenticate = new AuthUserPass(Server, Username, Password);
					break;
				default:
					throw new ProtocolViolationException();
			}
			authenticate.Authenticate();
		}
		/// <summary>
		/// Creates an array of bytes that has to be sent when the user wants to connect to a specific IPEndPoint.
		/// </summary>
		/// <param name="remoteEP">The IPEndPoint to connect to.</param>
		/// <returns>An array of bytes that has to be sent when the user wants to connect to a specific IPEndPoint.</returns>
		/// <exception cref="ArgumentNullException"><c>remoteEP</c> is null.</exception>
		private byte[] GetEndPointBytes() {
			byte [] connect = new byte[10];
            Array.Clear(connect, 0, connect.Length);
			connect[0] = 5;
			connect[1] = 3;
			connect[2] = 0; //reserved
			connect[3] = 1;
			//Array.Copy(AddressToBytes(remoteEP.Address.Address), 0, connect, 4, 4);
			//Array.Copy(remoteEP.Address.GetAddressBytes(), 0, connect, 4, 4);
			Array.Copy(PortToBytes(localPort), 0, connect, 8, 2);
			return connect;
		}
		/// <summary>
		/// Starts negotiating with the SOCKS server.
		/// </summary>
		/// <param name="host">The host to connect to.</param>
		/// <param name="port">The port to connect to.</param>
		/// <exception cref="ArgumentNullException"><c>host</c> is null.</exception>
		/// <exception cref="ArgumentException"><c>port</c> is invalid.</exception>
		/// <exception cref="ProxyException">The proxy rejected the request.</exception>
		/// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
		/// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
		/// <exception cref="ProtocolViolationException">The proxy server uses an invalid protocol.</exception>
		public override void Negotiate(string host, int port) {
            Negotiate(GetEndPointBytes());
		}
		/// <summary>
		/// Starts negotiating with the SOCKS server.
		/// </summary>
		/// <param name="remoteEP">The IPEndPoint to connect to.</param>
		/// <exception cref="ArgumentNullException"><c>remoteEP</c> is null.</exception>
		/// <exception cref="ProxyException">The proxy rejected the request.</exception>
		/// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
		/// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
		/// <exception cref="ProtocolViolationException">The proxy server uses an invalid protocol.</exception>
		public override void Negotiate(IPEndPoint remoteEP) {
            Negotiate(GetEndPointBytes());
		}
		/// <summary>
		/// Starts negotiating with the SOCKS server.
		/// </summary>
		/// <param name="connect">The bytes to send when trying to authenticate.</param>
		/// <exception cref="ArgumentNullException"><c>connect</c> is null.</exception>
		/// <exception cref="ArgumentException"><c>connect</c> is too small.</exception>
		/// <exception cref="ProxyException">The proxy rejected the request.</exception>
		/// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
		/// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
		/// <exception cref="ProtocolViolationException">The proxy server uses an invalid protocol.</exception>
		private void Negotiate(byte[] connect) {
			Authenticate();
			Server.Send(connect);
			byte[] buffer = ReadBytes(4);
			if (buffer[1] != 0) {
				Server.Close();
				throw new NotSupportedException("UDP");
			}
			switch(buffer[3]) {
                case 4:
				case 1:
                    {
                        var addrlen = buffer[3] == 1 ? 4 : 16;

                        buffer = ReadBytes(addrlen); //IPv4 address with port
                        var ipAddress = new IPAddress(buffer);

                        buffer = ReadBytes(2);
                        var port = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 0));

                        udpClient.Connect(ipAddress, port);
                    }
                    break;
				case 3:
                    {
                        buffer = ReadBytes(1);
                        buffer = ReadBytes(buffer[0]); //domain name with port

                        var address = Encoding.UTF8.GetString(buffer);

                        buffer = ReadBytes(2);
                        var port = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 0));

                        udpClient.Connect(address, port);
                    }
					break;

                default:
					Server.Close();
					throw new ProtocolViolationException();
			}
		}
		/// <summary>
		/// Starts negotiating asynchronously with the SOCKS server. 
		/// </summary>
		/// <param name="host">The host to connect to.</param>
		/// <param name="port">The port to connect to.</param>
		/// <param name="callback">The method to call when the negotiation is complete.</param>
		/// <param name="proxyEndPoint">The IPEndPoint of the SOCKS proxy server.</param>
		/// <returns>An IAsyncProxyResult that references the asynchronous connection.</returns>
		public override IAsyncProxyResult BeginNegotiate(string host, int port, HandShakeComplete callback, IPEndPoint proxyEndPoint) {
			ProtocolComplete = callback;
			HandShake = GetEndPointBytes();
			Server.BeginConnect(proxyEndPoint, new AsyncCallback(this.OnConnect), Server);
			AsyncResult = new IAsyncProxyResult();
			return AsyncResult;
		}
		/// <summary>
		/// Starts negotiating asynchronously with the SOCKS server. 
		/// </summary>
		/// <param name="remoteEP">An IPEndPoint that represents the remote device.</param>
		/// <param name="callback">The method to call when the negotiation is complete.</param>
		/// <param name="proxyEndPoint">The IPEndPoint of the SOCKS proxy server.</param>
		/// <returns>An IAsyncProxyResult that references the asynchronous connection.</returns>
		public override IAsyncProxyResult BeginNegotiate(IPEndPoint remoteEP, HandShakeComplete callback, IPEndPoint proxyEndPoint) {
			ProtocolComplete = callback;
			HandShake = GetEndPointBytes();
			Server.BeginConnect(proxyEndPoint, new AsyncCallback(this.OnConnect), Server);
			AsyncResult = new IAsyncProxyResult();
			return AsyncResult;
		}
		/// <summary>
		/// Called when the socket is connected to the remote server.
		/// </summary>
		/// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
		private void OnConnect(IAsyncResult ar) {
			try {
				Server.EndConnect(ar);
			} catch (Exception e) {
				ProtocolComplete(e);
				return;
			}
			try {
				Server.BeginSend(new byte [] {5, 2, 0, 2}, 0, 4, SocketFlags.None, new AsyncCallback(this.OnAuthSent), Server);
			} catch (Exception e) {
				ProtocolComplete(e);
			}
		}
		/// <summary>
		/// Called when the authentication bytes have been sent.
		/// </summary>
		/// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
		private void OnAuthSent(IAsyncResult ar) {
			try {
				Server.EndSend(ar);
			} catch (Exception e) {
				ProtocolComplete(e);
				return;
			}
			try {
				Buffer = new byte[1024];
				Received = 0;
				Server.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnAuthReceive), Server);
			} catch (Exception e) {
				ProtocolComplete(e);
			}
		}
		/// <summary>
		/// Called when an authentication reply has been received.
		/// </summary>
		/// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
		private void OnAuthReceive(IAsyncResult ar) {
			try {
				Received += Server.EndReceive(ar);
				if (Received <= 0)
					throw new SocketException();
			} catch (Exception e) {
				ProtocolComplete(e);
				return;
			}
			try {
				if (Received < 2) {
					Server.BeginReceive(Buffer, Received, Buffer.Length - Received, SocketFlags.None, new AsyncCallback(this.OnAuthReceive), Server);
				} else {
					AuthMethod authenticate;
					switch(Buffer[1]) {
						case 0:
							authenticate = new AuthNone(Server);
							break;
						case 2:
							authenticate = new AuthUserPass(Server, Username, Password);
							break;
						default:
							ProtocolComplete(new SocketException());
							return;
					}
					authenticate.BeginAuthenticate(new HandShakeComplete(this.OnAuthenticated));
				}
			} catch (Exception e) {
				ProtocolComplete(e);
			}
		}
		/// <summary>
		/// Called when the socket has been successfully authenticated with the server.
		/// </summary>
		/// <param name="e">The exception that has occured while authenticating, or <em>null</em> if no error occured.</param>
		private void OnAuthenticated(Exception e) {
			if (e != null) {
				ProtocolComplete(e);
				return;
			}
			try {
				Server.BeginSend(HandShake, 0, HandShake.Length, SocketFlags.None, new AsyncCallback(this.OnSent), Server);
			} catch (Exception ex) {
				ProtocolComplete(ex);
			}
		}
		/// <summary>
		/// Called when the connection request has been sent.
		/// </summary>
		/// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
		private void OnSent(IAsyncResult ar) {
			try {
				Server.EndSend(ar);
			} catch (Exception e) {
				ProtocolComplete(e);
				return;
			}
			try {
				Buffer = new byte[5];
				Received = 0;
				Server.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnReceive), Server);
			} catch (Exception e) {
				ProtocolComplete(e);
			}
		}
		/// <summary>
		/// Called when a connection reply has been received.
		/// </summary>
		/// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
		private void OnReceive(IAsyncResult ar) {
			try {
				Received += Server.EndReceive(ar);
			} catch (Exception e) {
				ProtocolComplete(e);
				return;
			}
			try {
				if (Received == Buffer.Length)
					ProcessReply(Buffer);
				else
					Server.BeginReceive(Buffer, Received, Buffer.Length - Received, SocketFlags.None, new AsyncCallback(this.OnReceive), Server);
			} catch (Exception e) {
				ProtocolComplete(e);
			}
		}
		/// <summary>
		/// Processes the received reply.
		/// </summary>
		/// <param name="buffer">The received reply</param>
		/// <exception cref="ProtocolViolationException">The received reply is invalid.</exception>
		private void ProcessReply(byte[] buffer) {
            if (buffer[1] != 0)
            {
                Server.Close();
                throw new NotSupportedException("UDP");
            }
            switch (buffer[3])
            {
                case 4:
                case 1:
                    {
                        var addrlen = buffer[3] == 1 ? 4 : 16;

                        buffer = ReadBytes(addrlen); //IPv4 address with port
                        var ipAddress = new IPAddress(buffer);

                        buffer = ReadBytes(2);
                        var port = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 0));

                        udpClient.Connect(ipAddress, port);
                    }
                    break;
                case 3:
                    {
                        buffer = ReadBytes(1);
                        buffer = ReadBytes(buffer[0]); //domain name with port

                        var address = Encoding.UTF8.GetString(buffer);

                        buffer = ReadBytes(2);
                        var port = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 0));

                        udpClient.Connect(address, port);
                    }
                    break;

                default:
                    Server.Close();
                    throw new ProtocolViolationException();
            }

			//Received = 0;
			//Server.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnReadLast), Server);
		}
		/// <summary>
		/// Called when the last bytes are read from the socket.
		/// </summary>
		/// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
		private void OnReadLast(IAsyncResult ar) {
			try {
				Received += Server.EndReceive(ar);
			} catch (Exception e) {
				ProtocolComplete(e);
				return;
			}
			try {
				if (Received == Buffer.Length)
					ProtocolComplete(null);
				else
					Server.BeginReceive(Buffer, Received, Buffer.Length - Received, SocketFlags.None, new AsyncCallback(this.OnReadLast), Server);
			} catch (Exception e) {
				ProtocolComplete(e);
			}
		}
		/// <summary>
		/// Gets or sets the password to use when authenticating with the SOCKS5 server.
		/// </summary>
		/// <value>The password to use when authenticating with the SOCKS5 server.</value>
		private string Password {
			get {
				return m_Password;
			}
			set {
				if (value == null)
					throw new ArgumentNullException();
				m_Password = value;
			}
		}
		/// <summary>
		/// Gets or sets the bytes to use when sending a connect request to the proxy server.
		/// </summary>
		/// <value>The array of bytes to use when sending a connect request to the proxy server.</value>
		private byte[] HandShake {
			get {
				return m_HandShake;
			}
			set {
				m_HandShake = value;
			}
		}
		// private variables
		/// <summary>Holds the value of the Password property.</summary>
		private string m_Password;
		/// <summary>Holds the value of the HandShake property.</summary>
		private byte[] m_HandShake;


        public override IAsyncResult UdpBeginReceive(AsyncCallback callback, object state) => udpClient.BeginReceive(callback, state);


        public override byte[] UdpEndReceive(IAsyncResult ar, ref IPEndPoint ep)
        {
            byte[] pkg = udpClient.EndReceive(ar, ref ep);
            var data = GetData(pkg, out IPEndPoint realep);
            ep = realep;
            return data;
        }

        private byte[] GetData(byte[] pkg, out IPEndPoint ep)
        {
            var port = new byte[2];
            IPAddress ipAddress;
            byte[] data;
            int ipPort;
            if (pkg[2] != 0)
            {
                Server.Close();
                throw new NotSupportedException("Not support FRAG");
            }
            switch (pkg[3])
            {
                case 4:
                case 1:
                    {
                        var addr =  new byte[pkg[3] == 1 ? 4 : 16];

                        System.Buffer.BlockCopy(pkg, 4, addr, 0, addr.Length);

                        ipAddress = new IPAddress(addr);

                        System.Buffer.BlockCopy(pkg, 4 + addr.Length, port, 0, port.Length);

                        ipPort = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(port, 0));

                        var dataOffset = 4 + addr.Length + port.Length;
                        data = new byte[pkg.Length - dataOffset];
                        System.Buffer.BlockCopy(pkg, dataOffset, data, 0, data.Length);
                    }
                    break;
                case 3:
                    {
                        var addr = new byte[pkg[4]];
                        System.Buffer.BlockCopy(pkg, 5, addr, 0, addr.Length);

                        var address = Encoding.UTF8.GetString(addr);
                        ipAddress = Dns.GetHostAddresses(address).First();

                        System.Buffer.BlockCopy(pkg, 5 + addr.Length, port, 0, port.Length);
                        ipPort = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(port, 0));

                        var dataOffset = 5 + addr.Length + port.Length;
                        data = new byte[pkg.Length - dataOffset];
                        System.Buffer.BlockCopy(pkg, dataOffset, data, 0, data.Length);
                    }
                    break;

                default:
                    Server.Close();
                    throw new ProtocolViolationException();
            }

            ep = new IPEndPoint(ipAddress, ipPort);
            return data;
        }

        public override int UdpSend(byte[] buffer, IPEndPoint ep)
        {
            byte[] data = new byte[10 + buffer.Length];
            data[0] = 0;
            data[1] = 0;
            data[2] = 0;
            data[3] = 1;
            System.Buffer.BlockCopy(ep.Address.GetAddressBytes(), 0, data, 4, 4);
            System.Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)ep.Port)), 0, data, 8, 2);
            System.Buffer.BlockCopy(buffer, 0, data, 10, buffer.Length);
            return udpClient.Send(data, data.Length);
        }

        public override void Close()
        {
            udpClient.Close();
        }

        public override IPEndPoint UdpEndPoint => (IPEndPoint)udpClient.Client.LocalEndPoint;

    }


}

#endif
