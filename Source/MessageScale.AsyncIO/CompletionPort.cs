using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security;
using System.Text;


namespace MessageScale.AsyncIO
{
    public class CompletionStatus
    {
        public CompletionStatus(OverlappedSocket socket, object state, OperationType operationType, SocketError socketError, int bytesTransferred)
        {
            Socket = socket;
            State = state;
            OperationType = operationType;
            SocketError = socketError;
            BytesTransferred = bytesTransferred;
        }

        public OverlappedSocket Socket { get; private set; }
        public object State { get; private set; }
        public OperationType OperationType { get; set; }

        public SocketError SocketError { get; private set; }
        public int BytesTransferred { get; private set; }        
    }
   
    public abstract class CompletionPort : IDisposable
    {
        public static CompletionPort Create()
        {
            return new MessageScale.AsyncIO.Windows.CompletionPort();
        }

        public abstract void Dispose();

        public abstract CompletionStatus GetQueuedCompletionStatus(int timeout);

        public abstract void AssociateSocket(OverlappedSocket socket, object state);

        public abstract void Signal();
    }
}
