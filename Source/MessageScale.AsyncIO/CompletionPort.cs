using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security;
using System.Text;


namespace MessageScale.AsyncIO
{
    public struct CompletionStatus
    {
        internal CompletionStatus(OverlappedSocket socket, object state, OperationType operationType, SocketError socketError, int bytesTransferred) : 
            this()
        {
            Socket = socket;
            State = state;
            OperationType = operationType;
            SocketError = socketError;
            BytesTransferred = bytesTransferred;
        }

        public OverlappedSocket Socket { get; internal set; }
        public object State { get; internal set; }
        public OperationType OperationType { get; internal set; }

        public SocketError SocketError { get; internal set; }
        public int BytesTransferred { get; internal set; }        
    }    
   
    public abstract class CompletionPort : IDisposable
    {
        public static CompletionPort Create()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || ForceDotNet.Forced)
            {
                return new MessageScale.AsyncIO.DotNet.CompletionPort();
            }
            else
            {
                return new MessageScale.AsyncIO.Windows.CompletionPort();
                
            }                   
        }

        public abstract void Dispose();

        public abstract bool GetQueuedCompletionStatus(int timeout, out CompletionStatus completionStatus);

        public abstract void AssociateSocket(OverlappedSocket socket, object state);

        public abstract void Signal();
    }
}
