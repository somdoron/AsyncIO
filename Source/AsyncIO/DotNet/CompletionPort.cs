using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace AsyncIO.DotNet
{
    class CompletionPort : AsyncIO.CompletionPort
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

        public override bool GetMultipleQueuedCompletionStatus(int timeout, CompletionStatus[] completionStatuses, out int removed)
        {
            removed = 0;

            if (m_queue.TryTake(out completionStatuses[0], timeout))
            {
                removed++;

                while (removed < completionStatuses.Length && m_queue.TryTake(out completionStatuses[removed], 0))
                {
                    removed++;
                }

                return true;
            }
            
            return false;
        }

        public override void AssociateSocket(AsyncSocket asyncSocket, object state)
        {
            var nativeSocket = (NativeSocket)asyncSocket;
            nativeSocket.SetCompletionPort(this, state);
        }

        public override void Signal(object state)
        {
            m_queue.Add(new CompletionStatus(null, state, OperationType.Signal, SocketError.Success, 0));
        }
    }
}
