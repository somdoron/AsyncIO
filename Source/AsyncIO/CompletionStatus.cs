using System.Net.Sockets;

namespace AsyncIO
{
    public struct CompletionStatus
    {
        internal CompletionStatus(AsyncSocket asyncSocket, object state, OperationType operationType, SocketError socketError, int bytesTransferred) : 
            this()
        {
            AsyncSocket = asyncSocket;
            State = state;
            OperationType = operationType;
            SocketError = socketError;
            BytesTransferred = bytesTransferred;
        }

        public AsyncSocket AsyncSocket { get; private set; }
        public object State { get; internal set; }
        public OperationType OperationType { get; internal set; }

        public SocketError SocketError { get; internal set; }
        public int BytesTransferred { get; internal set; }        
    }
}