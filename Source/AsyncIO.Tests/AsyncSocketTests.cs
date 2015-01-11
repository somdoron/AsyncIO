using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncIO.Tests
{    
    [TestFixture(true)]
    [TestFixture(false)]
    public class AsyncSocketTests
    {
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

            byte[] bytes = new byte[12];                        

            //bytes.PutInteger(endian, tcpKeepalive, 0);
            //bytes.PutInteger(endian, tcpKeepaliveIdle, 4);
            //bytes.PutInteger(endian, tcpKeepaliveIntvl, 8);

            socket.IOControl(IOControlCode.KeepAliveValues, (byte[])bytes, null);
        }
    }
}
