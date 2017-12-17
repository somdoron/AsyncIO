AsyncIO
========

AsyncIO is a portable high performance sockets library for .Net. The library is based on Windows IO Completion ports.

The .Net Socket library doesn't give control over the threads and doesn't expose the IO completion port API. AsyncIO give full control over the threads, which allows the developer to create high performance servers.

On Mono, the library calls pass through to the mono implementation, which still results in a completion-port-like API.

## Installation

You can install AsyncIO from [NuGet](http://www.nuget.org/packages/AsyncIO/).

## Using

Using AsyncIO is very similiar to using .Net Socket, to get the completion event of the operation you need to call `GetQueuedCompletionStatus` method of the completion port.

```csharp
static void Main(string[] args)
{
    CompletionPort completionPort = CompletionPort.Create();

    AutoResetEvent listenerEvent = new AutoResetEvent(false);
    AutoResetEvent clientEvent = new AutoResetEvent(false);
    AutoResetEvent serverEvent = new AutoResetEvent(false);

    AsyncSocket listener = AsyncSocket.Create(AddressFamily.InterNetwork, 
        SocketType.Stream, ProtocolType.Tcp);
    completionPort.AssociateSocket(listener, listenerEvent);

    AsyncSocket server = AsyncSocket.Create(AddressFamily.InterNetwork, 
        SocketType.Stream, ProtocolType.Tcp);
    completionPort.AssociateSocket(server, serverEvent);

    AsyncSocket client = AsyncSocket.Create(AddressFamily.InterNetwork, 
        SocketType.Stream, ProtocolType.Tcp);
    completionPort.AssociateSocket(client, clientEvent);

    Task.Factory.StartNew(() =>
    {
        CompletionStatus completionStatus;

        while (true)
        {
            var result = completionPort.GetQueuedCompletionStatus(-1, out completionStatus);

            if (result)
            {
                Console.WriteLine("{0} {1} {2}", completionStatus.SocketError, 
                    completionStatus.OperationType, completionStatus.BytesTransferred);

                if (completionStatus.State != null)
                {
                    AutoResetEvent resetEvent = (AutoResetEvent)completionStatus.State;
                    resetEvent.Set();
                }
            }
        }
    });

    listener.Bind(IPAddress.Any, 5555);
    listener.Listen(1);

    client.Connect("localhost", 5555);

    listener.Accept(server);


    listenerEvent.WaitOne();
    clientEvent.WaitOne();

    byte[] sendBuffer = new byte[1] { 2 };
    byte[] recvBuffer = new byte[1];

    client.Send(sendBuffer);
    server.Receive(recvBuffer);

    clientEvent.WaitOne();
    serverEvent.WaitOne();

    server.Dispose();
    client.Dispose();
}
```

## Compiling

To compile:

* Download and install [.NET Core SDK v2.1.2](https://www.microsoft.com/net/download/windows).
* Compile with [Visual Studio 2017 v15.5.2](https://docs.microsoft.com/en-us/visualstudio/install/install-visual-studio) or higher.

On compile, the NuGet package will be created in the `\bin\Release` directory. 

To test the compiled NuGet package, in Visual Studio 2017 options under `Visual Studio Package Manager`, you can add the `\bin\Release` directory with the NuGet package in it. This NuGet package will then be available to install in other projects.

