using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MessageScale.AsyncIO
{
    public static class SocketExtensions
    {
        public static void Bind(this OverlappedSocket socket, IPAddress ipAddress, int port)
        {
            socket.Bind(new IPEndPoint(ipAddress, port));
        }

        public static OperationResult Connect(this OverlappedSocket socket, IPAddress ipAddress, int port)
        {
            return socket.Connect(new IPEndPoint(ipAddress, port));
        }

        /// <summary>
        /// Connect to the first ip address of the host the match the address family
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="host">Host name to resolve</param>
        /// <param name="port">Port to connect</param>
        public static OperationResult Connect(this OverlappedSocket socket, string host, int port)
        {
            if (host == null)
                throw new ArgumentNullException("host");

            if (port < 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException("port");
            }

            var ipAddress = Dns.GetHostAddresses(host).FirstOrDefault(ip=>                 
                ip.AddressFamily == socket.AddressFamily || 
                (socket.AddressFamily == AddressFamily.InterNetworkV6 && socket.DualMode && ip.AddressFamily == AddressFamily.InterNetwork));

            if (ipAddress != null)
            {
                return socket.Connect(ipAddress, port);
            }
            else
            {
                throw new ArgumentException("invalid host", "host");
            }            
        }

        public static OperationResult Send(this OverlappedSocket socket, byte[] buffer)
        {
            return socket.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }

        public static OperationResult Receive(this OverlappedSocket socket, byte[] buffer, out int bytesTransferred)
        {
            return socket.Receive(buffer, 0, buffer.Length, SocketFlags.None, out bytesTransferred);
        }
    }
}
