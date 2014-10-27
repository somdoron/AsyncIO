using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MessageScale.AsyncIO.IOCP
{
    public class IOCPCompletionPort : ICompletionPort
    {
        private readonly IntPtr m_completionPortHandle;

        private readonly IntPtr InvalidCompletionPort = IntPtr.Zero;

        private readonly IntPtr InvalidCompletionPortMinusOne = new IntPtr(-1);

        private readonly UIntPtr SignalPostCompletionKey = new UIntPtr(1);
        
        private int m_completionKeySequence = 2;

        private const int WaitTimeoutError = 258;
        private const int CompletionPortClosed = 735;

        private bool m_disposed = false;

        public IOCPCompletionPort(uint concurrentThreads = 1)
        {
            m_completionPortHandle =
              UnsafeMethods.CreateIoCompletionPort(UnsafeMethods.INVALID_HANDLE_VALUE, InvalidCompletionPort, UIntPtr.Zero, concurrentThreads);

            if (m_completionPortHandle == InvalidCompletionPort || m_completionPortHandle == InvalidCompletionPortMinusOne)
            {
                throw new Win32Exception();
            }
        }

        ~IOCPCompletionPort()
        {
            Dispose();
        }

        internal void AssociateSocket(IOCPSocket socket)
       {
            int completionKey = Interlocked.Increment(ref m_completionKeySequence);

            IntPtr result = UnsafeMethods.CreateIoCompletionPort(socket.Handle,
                m_completionPortHandle, new UIntPtr((uint)completionKey), 0);

            if (result == InvalidCompletionPort || result == InvalidCompletionPortMinusOne)
            {
                throw new Win32Exception();
            }
        }

        public CompletionStatus GetQueuedCompletionStatus(int timeout)
        {
            uint numberOfBytes;
            UIntPtr completionKey;
            IntPtr overlapped;

            bool result = UnsafeMethods.GetQueuedCompletionStatus(m_completionPortHandle, out numberOfBytes, 
                out completionKey, out overlapped, timeout);

            if (!result)
            {
                int error = Marshal.GetLastWin32Error();

                if (error == CompletionPortClosed)
                {
                    throw new CompletionPortClosedException();
                }
                else if (error != WaitTimeoutError)
                {
                    throw new Win32Exception(error);
                }

                throw new TimeoutException();
            }
            else
            {
                if (completionKey.Equals(SignalPostCompletionKey))
                {
                    return new CompletionStatus(null, OperationType.Signal, SocketError.Success, 0);
                }
                else
                {
                    SocketError socketError;
                    OperationType operationType;
                    int bytesTransferred;
                    Overlapped.Read(overlapped, out operationType, out socketError, out bytesTransferred);

                    return new CompletionStatus(null, operationType, socketError, bytesTransferred);
                }
            }            
        }

      
        public void Signal()
        {
            UnsafeMethods.PostQueuedCompletionStatus(m_completionPortHandle, 0, SignalPostCompletionKey, IntPtr.Zero);
        }

        public void Dispose()
        {
            if (!m_disposed)
            {
                m_disposed = true;

                UnsafeMethods.CloseHandle(m_completionPortHandle);

                GC.SuppressFinalize(this);
            }
        }
    }
}
