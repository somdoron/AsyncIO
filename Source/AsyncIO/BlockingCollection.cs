#if NET35

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AsyncIO;

namespace System.Collections.Concurrent
{
    class BlockingCollection<T>
    {
        private Queue<T> m_queue;

        public BlockingCollection()
        {
            m_queue = new Queue<T>();
        }

        public void Add(T item)
        {
            lock (m_queue)
            {
                m_queue.Enqueue(item);
                if (m_queue.Count == 1)
                {
                    Monitor.PulseAll(m_queue);
                }
            }
        }

        public bool TryTake(out T item, int timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();            

            lock (m_queue)
            {
                while (m_queue.Count == 0)
                {
                    long elapsed = stopwatch.ElapsedMilliseconds;
                    int timeoutLeft = timeout == -1 ? -1 :
                     (elapsed > timeout ? 0 : timeout - (int)elapsed);

                    if (timeoutLeft == 0)
                    {
                        item = default(T);                        
                        return false;
                    }

                    Monitor.Wait(m_queue, timeoutLeft);
                }

                item = m_queue.Dequeue();                
                return true;
            }         
        }
    }
}
#endif