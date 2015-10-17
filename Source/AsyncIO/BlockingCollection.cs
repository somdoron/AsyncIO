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

            int timeoutLeft = timeout == -1 ? -1 :
                    (stopwatch.ElapsedMilliseconds > timeout ? 0 : timeout - (int)stopwatch.ElapsedMilliseconds);

            if (Monitor.TryEnter(m_queue, timeoutLeft))
            {
                while (m_queue.Count == 0)
                {
                    timeoutLeft = timeout == -1 ? -1 :
                     (stopwatch.ElapsedMilliseconds > timeout ? 0 : timeout - (int)stopwatch.ElapsedMilliseconds);

                    if (timeoutLeft == 0)
                    {
                        item = default(T);
                        Monitor.Exit(m_queue);
                        return false;
                    }

                    Monitor.Wait(m_queue, timeoutLeft);
                }

                item = m_queue.Dequeue();
                Monitor.Exit(m_queue);
                return true;
            }
            else
            {
                item = default(T);
                return false;
            }
        }
    }
}
