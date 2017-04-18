using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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

        private const SocketError ConnectionAborted = (SocketError)1236;
        private const SocketError NetworkNameDeleted = (SocketError)64;

        private bool m_disposed = false;

        private ConcurrentQueue<object> m_signalQueue;

        private OverlappedEntry[] m_overlappedEntries;
        private GCHandle m_overlappedEntriesHandle;
        private IntPtr m_overlappedEntriesAddress;

        public CompletionPort()
        {
            m_completionPortHandle =
              UnsafeMethods.CreateIoCompletionPort(UnsafeMethods.INVALID_HANDLE_VALUE, InvalidCompletionPort, IntPtr.Zero, 1);

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
                  SocketCompletionKey, 0);

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

        public override bool GetMultipleQueuedCompletionStatus(int timeout, CompletionStatus[] completionStatuses, out int removed)
        {
            // Windows XP Has NO GetQueuedCompletionStatusEx
            // so we need dequeue IOPC one by one
#if NETSTANDARD1_3
            if (false)
            {
                
            }
#else
            if (Environment.OSVersion.Version.Major == 5)
            {
                removed = 0;
                CompletionStatus completeStatus;
                bool result = GetQueuedCompletionStatus(timeout, out completeStatus);
                if (result)
                {
                    completionStatuses[0] = completeStatus;
                    removed = 1;
                }
                return result;
            }
#endif
            else
            {
                if (m_overlappedEntries == null || m_overlappedEntries.Length < completionStatuses.Length)
                {
                    if (m_overlappedEntries != null)
                    {
                        m_overlappedEntriesHandle.Free();
                    }

                    m_overlappedEntries = new OverlappedEntry[completionStatuses.Length];

                    m_overlappedEntriesHandle = GCHandle.Alloc(m_overlappedEntries, GCHandleType.Pinned);
                    m_overlappedEntriesAddress = Marshal.UnsafeAddrOfPinnedArrayElement(m_overlappedEntries, 0);
                }

                bool result = UnsafeMethods.GetQueuedCompletionStatusEx(m_completionPortHandle,
                                                                        m_overlappedEntriesAddress,
                                                                        completionStatuses.Length, out removed, timeout,
                                                                        false);

                if (!result)
                {
                    int error = Marshal.GetLastWin32Error();

                    if (error == WaitTimeoutError)
                    {
                        removed = 0;
                        return false;
                    }

                    throw new Win32Exception(error);
                }

                for (int i = 0; i < removed; i++)
                {
                    HandleCompletionStatus(out completionStatuses[i], m_overlappedEntries[i].Overlapped,
                                           m_overlappedEntries[i].CompletionKey, m_overlappedEntries[i].BytesTransferred);
                }
            }
            return true;
        }

        public override bool GetQueuedCompletionStatus(int timeout, out CompletionStatus completionStatus)
        {
            int bytesTransferred;
            IntPtr completionKey;
            IntPtr overlappedAddress;

            bool result = UnsafeMethods.GetQueuedCompletionStatus(m_completionPortHandle, out bytesTransferred,
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
            
            HandleCompletionStatus(out completionStatus, overlappedAddress, completionKey, bytesTransferred);
                        
            return true;
        }

        private void HandleCompletionStatus(out CompletionStatus completionStatus, IntPtr overlappedAddress, IntPtr completionKey, int bytesTransferred)
        {
            if (completionKey.Equals(SignalPostCompletionKey))
            {
                object state;

                m_signalQueue.TryDequeue(out state);
                completionStatus = new CompletionStatus(null, state, OperationType.Signal, SocketError.Success, 0);
            }
            else
            {

                var overlapped = Overlapped.CompleteOperation(overlappedAddress);

                if (completionKey.Equals(SocketCompletionKey))
                {
                    // if the overlapped ntstatus is zero we assume success and don't call get overlapped result for optimization
                    if (overlapped.Success)
                    {
                        SocketError socketError = SocketError.Success;
                        try
                        {
                            if (overlapped.OperationType == OperationType.Accept)
                            {
                                overlapped.AsyncSocket.UpdateAccept();
                            }
                            else if (overlapped.OperationType == OperationType.Connect)
                            {
                                overlapped.AsyncSocket.UpdateConnect();
                            }
                        }
                        catch (SocketException ex)
                        {
                            socketError = ex.SocketErrorCode;
                        }

                        completionStatus = new CompletionStatus(overlapped.AsyncSocket, overlapped.State,
                            overlapped.OperationType, socketError,
                            bytesTransferred);
                    }
                    else
                    {
                        SocketError socketError = SocketError.Success;
                        SocketFlags socketFlags;
                        if(overlapped.Disposed)
                        {
                            socketError = SocketError.OperationAborted;
                        }
                        else
                        {
                            bool operationSucceed = UnsafeMethods.WSAGetOverlappedResult(overlapped.AsyncSocket.Handle,
                                overlappedAddress,
                                out bytesTransferred, false, out socketFlags);

                            if (!operationSucceed)
                            {
                                socketError = (SocketError)Marshal.GetLastWin32Error();
                            }

                        }
                        completionStatus = new CompletionStatus(overlapped.AsyncSocket, overlapped.State,
                            overlapped.OperationType, socketError,
                            bytesTransferred);
                    }
                }
                else
                {
                    completionStatus = new CompletionStatus(overlapped.AsyncSocket, overlapped.State,
                        overlapped.OperationType, SocketError.Success, 0);
                }
            }
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

                if (m_overlappedEntries != null)
                {
                    m_overlappedEntries = null;
                    m_overlappedEntriesHandle.Free();
                }

                UnsafeMethods.CloseHandle(m_completionPortHandle);

                GC.SuppressFinalize(this);
            }
        }
    }
}
