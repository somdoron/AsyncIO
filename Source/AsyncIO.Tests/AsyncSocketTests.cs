using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncIO.Tests
{    
    [TestFixture(true)]
    [TestFixture(false)]
    public class AsyncSocketTests
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct tcp_keepalive
        {
            internal uint onoff;
            internal uint keepalivetime;
            internal uint keepaliveinterval;
        };

        public AsyncSocketTests(bool forceDotNet)
        {
            if (forceDotNet)
            {
                ForceDotNet.Force();
            }
            else
            {
                ForceDotNet.Unforce();
            }
        }

        [Test]
        public void KeepAlive()
        {
            var socket = AsyncSocket.Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            tcp_keepalive tcpKeepalive  = new tcp_keepalive();
            tcpKeepalive.onoff = 1;
            tcpKeepalive.keepaliveinterval = 1000;
            tcpKeepalive.keepalivetime = 1000;

            int size = Marshal.SizeOf(tcpKeepalive);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(tcpKeepalive, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);


            socket.IOControl(IOControlCode.KeepAliveValues, (byte[])arr, null);
        }
    }
}
