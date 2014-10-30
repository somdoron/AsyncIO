using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncIO;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            ForceDotNet.Force();

            CompletionPort completionPort = CompletionPort.Create();
           
            AutoResetEvent listenerEvent = new AutoResetEvent(false);
            AutoResetEvent clientEvent = new AutoResetEvent(false);
            AutoResetEvent serverEvent = new AutoResetEvent(false);
            
            AsyncSocket listener = AsyncSocket.Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);            
            completionPort.AssociateSocket(listener, listenerEvent);          

            AsyncSocket server = AsyncSocket.Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);            
            completionPort.AssociateSocket(server, serverEvent);

            AsyncSocket client = AsyncSocket.Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);            
            completionPort.AssociateSocket(client, clientEvent);            

            Task.Factory.StartNew(() =>
            {
                CompletionStatus completionStatus;

                while (true)
                {
                    completionPort.GetQueuedCompletionStatus(-1, out completionStatus);

                    if (completionStatus.State != null)
                    {
                        AutoResetEvent resetEvent = (AutoResetEvent)completionStatus.State;
                        resetEvent.Set();
                    }

                    Console.WriteLine("{0} {1} {2}", completionStatus.SocketError, completionStatus.OperationType, 
                        completionStatus.BytesTransferred);
                }
            });

            listener.Bind(IPAddress.Any, 5555);
            listener.Listen(1);

            Console.WriteLine(listener.LocalEndPoint);

            Console.WriteLine(listener.Accept(server));

            //client.Bind(IPAddress.Any,0);
            Console.WriteLine(client.Connect("localhost", 5555));

            listenerEvent.WaitOne();
            clientEvent.WaitOne();

            byte[] sendBuffer= new byte[1] {2};
            byte[] recvBuffer = new byte[1];

            int read;
            
            Console.WriteLine(client.Send(sendBuffer));
            Console.WriteLine("{0} {1}", server.Receive(recvBuffer, out read), read);

            clientEvent.WaitOne();
            serverEvent.WaitOne();

            Console.ReadLine();
        }
    }
}
