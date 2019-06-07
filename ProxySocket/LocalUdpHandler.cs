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
	internal sealed class LocalUdpHandler : SocksHandler {

        UdpClient udpClient;
        int localPort;

		/// <summary>
		/// Initiliazes a new Socks5Handler instance.
		/// </summary>
		/// <param name="server">The socket connection with the proxy server.</param>
		/// <exception cref="ArgumentNullException"><c>server</c>  is null.</exception>
		public LocalUdpHandler() : base(null, "") {
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            localPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
        }

		public override void Negotiate(string host, int port) {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }
        /// <summary>
        /// Starts negotiating asynchronously with the SOCKS server. 
        /// </summary>
        /// <param name="remoteEP">An IPEndPoint that represents the remote device.</param>
        /// <param name="callback">The method to call when the negotiation is complete.</param>
        /// <param name="proxyEndPoint">The IPEndPoint of the SOCKS proxy server.</param>
        /// <returns>An IAsyncProxyResult that references the asynchronous connection.</returns>
        public override IAsyncProxyResult BeginNegotiate(IPEndPoint remoteEP, HandShakeComplete callback, IPEndPoint proxyEndPoint) {
            throw new NotSupportedException();
        }


        public override IAsyncResult UdpBeginReceive(AsyncCallback callback, object state) => udpClient.BeginReceive(callback, state);

        public override byte[] UdpEndReceive(IAsyncResult ar, ref IPEndPoint ep)
        {
            byte[] pkg = udpClient.EndReceive(ar, ref ep);
            return pkg;
        }

        public override int UdpSend(byte[] buffer, IPEndPoint ep)
        {
            return udpClient.Send(buffer, buffer.Length, ep);
        }

        public override void Close()
        {
            udpClient.Close();
        }

        public override IPEndPoint UdpEndPoint => (IPEndPoint)udpClient.Client.LocalEndPoint;
    }
}

#endif
