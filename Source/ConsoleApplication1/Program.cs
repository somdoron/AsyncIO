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
                CompletionStatus []completionStatuses = new CompletionStatus[10];

                int removed;

                while (true)
                {                    
                    var result = completionPort.GetMultipleQueuedCompletionStatus(-1, completionStatuses, out removed);

                    for (int i = 0; i < removed; i++)
                    {
                        var completionStatus = completionStatuses[i];

                        Console.WriteLine("{0} {1} {2}", completionStatus.SocketError, completionStatus.OperationType,
                            completionStatus.BytesTransferred);

                        if (completionStatus.State != null)
                        {
                            AutoResetEvent resetEvent = (AutoResetEvent)completionStatus.State;
                            resetEvent.Set();
                        }                        
                    }

                    Console.WriteLine("Handled {0} statuses", removed);

                    Thread.Sleep(100);
                }
            });

            listener.Bind(IPAddress.Any, 5555);
            listener.Listen(1);

            //Console.WriteLine(listener.LocalEndPoint);            

            client.Bind(IPAddress.Any,0);
            client.Connect("localhost", 5555);

            ////Thread.Sleep(100);

            listener.Accept(server);
            
            listenerEvent.WaitOne();
            clientEvent.WaitOne();

            byte[] sendBuffer = new byte[1] {2};
            byte[] recvBuffer = new byte[1];

            server.Receive(recvBuffer);
            client.Send(sendBuffer);          

            clientEvent.WaitOne();
            serverEvent.WaitOne();
            ////Console.WriteLine("server received");

            Console.ReadLine();
        }
    }
}
