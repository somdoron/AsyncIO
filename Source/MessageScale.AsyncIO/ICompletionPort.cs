using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security;
using System.Text;

namespace MessageScale.AsyncIO
{
    public class CompletionStatus
    {
        public CompletionStatus(ISocket socket, OperationType operationType, SocketError socketError, int bytesTransferred)
        {
            Socket = socket;
            OperationType = operationType;
            SocketError = socketError;
            BytesTransferred = bytesTransferred;
        }

        public ISocket Socket { get; private set; }
        public OperationType OperationType { get; set; }

        public SocketError SocketError { get; private set; }
        public int BytesTransferred { get; private set; }        
    }
   
    public interface ICompletionPort : IDisposable
    {
        CompletionStatus GetQueuedCompletionStatus(int timeout);

        void Signal();
    }
}
