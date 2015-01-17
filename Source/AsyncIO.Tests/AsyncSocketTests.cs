using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

        [Test]
        public void RemoteEndPoint()
        {
            CompletionPort completionPort = CompletionPort.Create();

            var listenSocket = AsyncSocket.CreateIPv4Tcp();
            completionPort.AssociateSocket(listenSocket, null);
            listenSocket.Bind(IPAddress.Any, 5553);
            listenSocket.Listen(1);
            
            var server = AsyncSocket.CreateIPv4Tcp();            
            
            listenSocket.Accept(server);

            var clientSocket = AsyncSocket.CreateIPv4Tcp();
            completionPort.AssociateSocket(clientSocket, null);
            clientSocket.Bind(IPAddress.Any, 0);
            clientSocket.Connect("localhost", 5553);

            CompletionStatus completionStatus;

            bool accepted = false;
            bool connected = false;

            while (!accepted || !connected) 
            {            
                completionPort.GetQueuedCompletionStatus(-1, out completionStatus);

                if (completionStatus.AsyncSocket == clientSocket && !connected)
                {
                    Assert.AreEqual(OperationType.Connect, completionStatus.OperationType);
                    Assert.AreEqual(SocketError.Success, completionStatus.SocketError);
                    connected = true;
                }
                else if (completionStatus.AsyncSocket == listenSocket && !accepted)
                {
                    Assert.AreEqual(OperationType.Accept, completionStatus.OperationType);
                    Assert.AreEqual(SocketError.Success, completionStatus.SocketError);
                    accepted = true;
                }
                else
                {
                    Assert.Fail();
                }
            }                        

            Assert.AreEqual(clientSocket.LocalEndPoint, server.RemoteEndPoint);
            Assert.AreEqual(clientSocket.RemoteEndPoint, server.LocalEndPoint);

            completionPort.Dispose();
            listenSocket.Dispose();
            server.Dispose();
            clientSocket.Dispose();
        }

        [Test]
        public void SendReceive()
        {
            CompletionPort completionPort = CompletionPort.Create();

            bool exception = false;

            var task = Task.Factory.StartNew(() =>
            {
                bool cancel = false;

                while (!cancel)
                {
                    CompletionStatus [] completionStatuses = new CompletionStatus[10];

                    int removed;

                    completionPort.GetMultipleQueuedCompletionStatus(-1, completionStatuses, out removed);

                    for (int i = 0; i < removed; i++)
                    {
                        if (completionStatuses[i].OperationType == OperationType.Signal)
                        {
                            cancel = true;
                        }
                        else if (completionStatuses[i].SocketError == SocketError.Success)
                        {
                            EventWaitHandle manualResetEvent = (EventWaitHandle)completionStatuses[i].State;
                            manualResetEvent.Set();
                        }
                        else
                        {
                            exception = true;
                        }
                    }
                }
            });

            AutoResetEvent clientEvent = new AutoResetEvent(false);
            AutoResetEvent acceptedEvent = new AutoResetEvent(false);

            AsyncSocket listener = AsyncSocket.CreateIPv4Tcp();
            completionPort.AssociateSocket(listener, acceptedEvent);            
            listener.Bind(IPAddress.Any, 5553);
            listener.Listen(1);
            
            AsyncSocket serverSocket = AsyncSocket.CreateIPv4Tcp();
            listener.Accept(serverSocket);

            AsyncSocket clientSocket = AsyncSocket.CreateIPv4Tcp();
            completionPort.AssociateSocket(clientSocket, clientEvent);
            clientSocket.Bind(IPAddress.Any,0);
            clientSocket.Connect("localhost", 5553);

            clientEvent.WaitOne();
            acceptedEvent.WaitOne();

            AutoResetEvent serverEvent = new AutoResetEvent(false);
            completionPort.AssociateSocket(serverSocket, serverEvent);

            byte[] recv = new byte[1];
            serverSocket.Receive(recv);

            byte[] data = new[] {(byte)1};
            
            clientSocket.Send(data);
            clientEvent.WaitOne(); // wait for data to be send

            serverEvent.WaitOne(); // wait for data to be received

            Assert.AreEqual(1, recv[0]);

            completionPort.Signal(null);
            task.Wait();

            Assert.IsFalse(exception);

            completionPort.Dispose();
            listener.Dispose();
            serverSocket.Dispose();
            clientSocket.Dispose();
        }
    }
}
