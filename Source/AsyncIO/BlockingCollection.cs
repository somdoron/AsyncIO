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

        private AutoResetEvent m_newItemEvent = new AutoResetEvent(false);

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
                    m_newItemEvent.Set();
                }
            }
        }        

        public bool TryTake(out T item, int timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                int timeoutLeft = timeout == -1 ? -1 : 
                        (stopwatch.ElapsedMilliseconds > timeout ? 0 : timeout - (int) stopwatch.ElapsedMilliseconds);                                        

                if (Monitor.TryEnter(m_queue, timeoutLeft))
                {
                    if (m_queue.Count > 0)
                    {
                        item = m_queue.Dequeue();
                        Monitor.Exit(m_queue);
                        return true;
                    }
                    else
                    {
                        Monitor.Exit(m_queue);

                        timeoutLeft = timeout == -1 ? -1 :
                            (stopwatch.ElapsedMilliseconds > timeout ? 0 : timeout - (int)stopwatch.ElapsedMilliseconds);  

                        if (!m_newItemEvent.WaitOne(timeoutLeft))
                        {
                            item = default(T);
                            return false;
                        }
                    }
                }
                else
                {
                    item = default(T);
                    return false;
                }
            }
        }
    }
}
