using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace MessageScale.AsyncIO.DotNet
{
    public class CompletionPort : AsyncIO.CompletionPort
    {
        private BlockingCollection<CompletionStatus> m_queue; 

        public CompletionPort()
        {
            m_queue = new BlockingCollection<CompletionStatus>();
        }

        internal void Queue(ref CompletionStatus completionStatus)
        {
            m_queue.Add(completionStatus);
        }

        public override void Dispose()
        {            
        }

        public override bool GetQueuedCompletionStatus(int timeout, out CompletionStatus completionStatus)
        {
            if (m_queue.TryTake(out completionStatus, timeout))
            {
                return true;
            }

            return false;            
        }

        public override void AssociateSocket(OverlappedSocket overlappedSocket, object state)
        {
            var nativeSocket = (NativeSocket)overlappedSocket;
            nativeSocket.SetCompletionPort(this, state);
        }

        public override void Signal()
        {
            m_queue.Add(new CompletionStatus(null,null, OperationType.Signal, SocketError.Success, 0));
        }
    }
}
