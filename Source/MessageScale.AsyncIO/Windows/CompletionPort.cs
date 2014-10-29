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

namespace MessageScale.AsyncIO.Windows
{
    class CompletionPort : AsyncIO.CompletionPort
    {
        struct SocketState
        {
            public OverlappedSocket Socket { get; set; }
            public object State { get; set; }
        }

        private readonly IntPtr m_completionPortHandle;

        private readonly IntPtr InvalidCompletionPort = IntPtr.Zero;

        private readonly IntPtr InvalidCompletionPortMinusOne = new IntPtr(-1);

        private readonly UIntPtr SignalPostCompletionKey = new UIntPtr(1);
        
        private int m_completionKeySequence = 2;

        private Dictionary<int, SocketState> m_sockets; 

        private const int WaitTimeoutError = 258;        

        private bool m_disposed = false;

        public CompletionPort()
        {
            m_completionPortHandle =
              UnsafeMethods.CreateIoCompletionPort(UnsafeMethods.INVALID_HANDLE_VALUE, InvalidCompletionPort, UIntPtr.Zero, 1);

            if (m_completionPortHandle == InvalidCompletionPort || m_completionPortHandle == InvalidCompletionPortMinusOne)
            {
                throw new Win32Exception();
            }

            m_sockets = new Dictionary<int, SocketState>();
        }

        ~CompletionPort()
        {
            Dispose();
        }        

        public override void AssociateSocket(OverlappedSocket socket, object state)
        {
            if (!(socket is Windows.Socket))
            {
                throw new ArgumentException("socket must be of type Windows.Socket", "socket");
            }

            Socket windowsSocket = socket as Socket;

            m_completionKeySequence++;
            
            int completionKey = m_completionKeySequence;            

            m_sockets.Add(completionKey, new SocketState {Socket = socket, State = state});
            windowsSocket.SetCompletionPort(this, completionKey);

            IntPtr result = UnsafeMethods.CreateIoCompletionPort(windowsSocket.Handle, m_completionPortHandle, new UIntPtr((uint)completionKey), 0);

            if (result == InvalidCompletionPort || result == InvalidCompletionPortMinusOne)
            {
                throw new Win32Exception();
            }
        }

        internal void RemoveSocket(int completionKey)
        {
            m_sockets.Remove(completionKey);
        }

        public override bool GetQueuedCompletionStatus(int timeout, out CompletionStatus completionStatus)
        {
            uint numberOfBytes;
            UIntPtr completionKey;
            IntPtr overlapped;

            bool result = UnsafeMethods.GetQueuedCompletionStatus(m_completionPortHandle, out numberOfBytes, 
                out completionKey, out overlapped, timeout);

            if (!result)
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
                    completionStatus = new CompletionStatus(null,null,OperationType.Signal, SocketError.Success, 0);                    
                }
                else
                {
                    SocketError socketError;
                    OperationType operationType;
                    int bytesTransferred;
                    Overlapped.Read(overlapped, out operationType, out socketError, out bytesTransferred);

                    var socketState = m_sockets[(int)completionKey];

                    completionStatus = new CompletionStatus( socketState.Socket, socketState.State, operationType, socketError, bytesTransferred);                    
                }
            }

            return true;
        }

        public override void Signal()
        {
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
