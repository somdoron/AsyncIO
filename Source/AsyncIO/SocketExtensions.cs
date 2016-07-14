using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AsyncIO
{
    public static class SocketExtensions
    {
        public static void Bind(this AsyncSocket socket, IPAddress ipAddress, int port)
        {
            socket.Bind(new IPEndPoint(ipAddress, port));
        }

        public static void Connect(this AsyncSocket socket, IPAddress ipAddress, int port)
        {
            socket.Connect(new IPEndPoint(ipAddress, port));
        }

        /// <summary>
        /// Connect to the first ip address of the host the match the address family
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="host">Host name to resolve</param>
        /// <param name="port">Port to connect</param>
        public static void Connect(this AsyncSocket socket, string host, int port)
        {
            if (host == null)
                throw new ArgumentNullException("host");

            if (port < 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException("port");
            }

#if NETSTANDARD1_3
            var ipAddress = Dns.GetHostAddressesAsync(host).Result.FirstOrDefault(ip=>                 
                ip.AddressFamily == socket.AddressFamily || 
                (socket.AddressFamily == AddressFamily.InterNetworkV6 && socket.DualMode && ip.AddressFamily == AddressFamily.InterNetwork));
#else            
            var ipAddress = Dns.GetHostAddresses(host).FirstOrDefault(ip=>                 
                ip.AddressFamily == socket.AddressFamily || 
                (socket.AddressFamily == AddressFamily.InterNetworkV6 && socket.DualMode && ip.AddressFamily == AddressFamily.InterNetwork));
#endif

            if (ipAddress != null)
            {
                socket.Connect(ipAddress, port);
            }
            else
            {
                throw new ArgumentException("invalid host", "host");
            }            
        }

        public static void Send(this AsyncSocket socket, byte[] buffer)
        {
            socket.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }

        public static void Receive(this AsyncSocket socket, byte[] buffer)
        {
            socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
        }
    }
}
