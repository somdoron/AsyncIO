#if NET35

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Collections.Concurrent
{

    class ConcurrentQueue<T>
    {
        private Queue<T> m_queue;

        public ConcurrentQueue()
        {
            m_queue = new Queue<T>();
        }

        internal void Enqueue(T state)
        {
            lock (m_queue)
            {
                m_queue.Enqueue(state);
            }
        }

        internal bool TryDequeue(out T state)
        {
            lock (m_queue)
            {
                if (m_queue.Count > 0)
                {
                    state = m_queue.Dequeue();
                    return true;    
                }
                else
                {
                    state = default (T);
                    return false;
                }
            }
        }
    }
}

#endif