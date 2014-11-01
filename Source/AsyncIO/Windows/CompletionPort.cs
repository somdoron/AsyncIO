using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AsyncIO.Windows
{
    class CompletionPort : AsyncIO.CompletionPort
    {
        private readonly IntPtr m_completionPortHandle;

        private readonly IntPtr InvalidCompletionPort = IntPtr.Zero;

        private readonly IntPtr InvalidCompletionPortMinusOne = new IntPtr(-1);

        private readonly IntPtr SignalPostCompletionKey = new IntPtr(1);
        private readonly IntPtr SocketCompletionKey = new IntPtr(2);
        private readonly IntPtr SocketManualCompletionKey = new IntPtr(3);

        private const int WaitTimeoutError = 258;

        private const SocketError ConnectionAborted = (SocketError) 1236;
        private const SocketError NetworkNameDeleted = (SocketError) 64;

        private bool m_disposed = false;

        private ConcurrentQueue<object> m_signalQueue;

        public CompletionPort()
        {
            m_completionPortHandle =
              UnsafeMethods.CreateIoCompletionPort(UnsafeMethods.INVALID_HANDLE_VALUE, InvalidCompletionPort, UIntPtr.Zero, 1);

            if (m_completionPortHandle == InvalidCompletionPort || m_completionPortHandle == InvalidCompletionPortMinusOne)
            {
                throw new Win32Exception();
            }

            m_signalQueue = new ConcurrentQueue<object>();
        }

        ~CompletionPort()
        {
            Dispose();
        }

        public override void AssociateSocket(AsyncSocket socket, object state)
        {
            if (!(socket is Windows.Socket))
            {
                throw new ArgumentException("socket must be of type Windows.Socket", "socket");
            }

            Socket windowsSocket = socket as Socket;

            if (windowsSocket.CompletionPort != this)
            {
                IntPtr result = UnsafeMethods.CreateIoCompletionPort(windowsSocket.Handle, m_completionPortHandle,
                  new UIntPtr((uint)SocketCompletionKey), 0);

                if (result == InvalidCompletionPort || result == InvalidCompletionPortMinusOne)
                {
                    throw new Win32Exception();
                }
            }

            windowsSocket.SetCompletionPort(this, state);
        }

        internal void PostCompletionStatus(IntPtr overlapped)
        {
            UnsafeMethods.PostQueuedCompletionStatus(m_completionPortHandle, 0, SocketManualCompletionKey, overlapped);
        }

        public override bool GetQueuedCompletionStatus(int timeout, out CompletionStatus completionStatus)
        {
            uint numberOfBytes;
            IntPtr completionKey;
            IntPtr overlappedAddress;

            bool result = UnsafeMethods.GetQueuedCompletionStatus(m_completionPortHandle, out numberOfBytes,
                out completionKey, out overlappedAddress, timeout);

            if (!result && overlappedAddress == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();

                if (error == WaitTimeoutError)
                {
                    completionStatus = new CompletionStatus();

                    return false;
                }

                throw new Win32Exception(error);
            }
            else
            {
                if (completionKey.Equals(SignalPostCompletionKey))
                {
                    object state;

                    m_signalQueue.TryDequeue(out state);
                    completionStatus = new CompletionStatus( state, OperationType.Signal, SocketError.Success, 0);
                }
                else
                {                                     
                    //if (!result)
                    //{
                    //    socketError = (SocketError)Marshal.GetLastWin32Error();

                    //    if (socketError == ConnectionAborted)
                    //    {
                    //        completionStatus = new CompletionStatus();
                    //        return false;
                    //    }
                    //    else if (socketError == NetworkNameDeleted)
                    //    {
                    //        socketError = SocketError.ConnectionReset;
                    //    }
                    //}                    

                    var overlapped = Overlapped.CompleteOperation(overlappedAddress);

                    if (completionKey.Equals(SocketCompletionKey))
                    {
                        int bytesTransferred;
                        SocketError socketError = SocketError.Success;
                        SocketFlags socketFlags;

                        bool operationSucceed = UnsafeMethods.WSAGetOverlappedResult(overlapped.AsyncSocket.Handle, overlappedAddress,
                            out bytesTransferred, false, out socketFlags);

                        if (!operationSucceed)
                        {
                            socketError = (SocketError)Marshal.GetLastWin32Error();
                        }

                        completionStatus = new CompletionStatus(overlapped.State, overlapped.OperationType, socketError, bytesTransferred);                    
                    }
                    else
                    {
                        completionStatus = new CompletionStatus(overlapped.State, overlapped.OperationType, SocketError.Success, 0);                    
                    }                    
                }
            }

            return true;
        }

        public override void Signal(object state)
        {
            m_signalQueue.Enqueue(state);
            UnsafeMethods.PostQueuedCompletionStatus(m_completionPortHandle, 0, SignalPostCompletionKey, IntPtr.Zero);
        }

        public override void Dispose()
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
