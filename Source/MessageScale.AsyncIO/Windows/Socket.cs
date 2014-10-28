using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MessageScale.AsyncIO.Windows
{
    internal class Socket : OverlappedSocket
    {
        private CompletionPort m_completionPort;
        private int m_completionKey;

        private Overlapped m_inOverlapped;
        private Overlapped m_outOverlapped;

        private ConnectExDelegate m_connectEx;
        private AcceptExDelegate m_acceptEx;
        private bool m_disposed;
        private SocketAddress m_boundAddress;
        private SocketAddress m_remoteAddress;

        private IntPtr m_acceptSocketBufferAddress;
        private int m_acceptSocketBufferSize;

        public Socket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : base(addressFamily, socketType, protocolType)
        {
            m_disposed = false;

            m_inOverlapped = new Overlapped();
            m_outOverlapped = new Overlapped();

            InitSocket();
            InitDynamicMethods();
        }

        ~Socket()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                m_disposed = true;

                m_inOverlapped.Dispose();
                m_outOverlapped.Dispose();

                if (m_remoteAddress != null)
                {
                    m_remoteAddress.Dispose();
                    m_remoteAddress = null;
                }

                if (m_boundAddress != null)
                {
                    m_boundAddress.Dispose();
                    m_boundAddress = null;
                }

                if (m_completionPort != null)
                {
                    m_completionPort.RemoveSocket(m_completionKey);
                    m_completionPort = null;
                }

                if (m_acceptSocketBufferAddress != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(m_acceptSocketBufferAddress);
                }

                UnsafeMethods.closesocket(Handle);
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IntPtr Handle { get; private set; }

        private void InitSocket()
        {
            Handle = UnsafeMethods.WSASocket(AddressFamily, SocketType, ProtocolType,
                IntPtr.Zero, 0, SocketConstructorFlags.WSA_FLAG_OVERLAPPED);

            if (Handle == UnsafeMethods.INVALID_HANDLE_VALUE)
            {
                throw new SocketException();
            }
        }

        private void InitDynamicMethods()
        {
            m_connectEx =
              (ConnectExDelegate)LoadDynamicMethod(UnsafeMethods.WSAID_CONNECTEX, typeof(ConnectExDelegate));

            m_acceptEx =
              (AcceptExDelegate)LoadDynamicMethod(UnsafeMethods.WSAID_ACCEPT_EX, typeof(AcceptExDelegate));
        }

        private Delegate LoadDynamicMethod(Guid guid, Type type)
        {
            IntPtr connectExAddress = IntPtr.Zero;
            int byteTransfered = 0;

            SocketError socketError = (SocketError)UnsafeMethods.WSAIoctl(Handle, UnsafeMethods.GetExtensionFunctionPointer,
                ref guid, Marshal.SizeOf(guid), ref connectExAddress, IntPtr.Size, ref byteTransfered, IntPtr.Zero, IntPtr.Zero);

            if (socketError != SocketError.Success)
            {
                throw new SocketException();
            }

            return Marshal.GetDelegateForFunctionPointer(connectExAddress, type);
        }

        internal void SetCompletionPort(CompletionPort completionPort, int completionKey)
        {
            if (m_completionPort != null)
            {
                m_completionPort.RemoveSocket(m_completionKey);
            }

            m_completionKey = completionKey;
            m_completionPort = completionPort;
        }

        public override void Bind(IPEndPoint localEndPoint)
        {
            if (m_boundAddress != null)
            {
                m_boundAddress.Dispose();
                m_boundAddress = null;
            }

            m_boundAddress = new SocketAddress(localEndPoint.Address, localEndPoint.Port);

            SocketError bindResult = (SocketError)UnsafeMethods.bind(Handle, m_boundAddress.Buffer, m_boundAddress.Size);

            if (bindResult != SocketError.Success)
            {
                throw new SocketException((int)bindResult);
            }
        }

        public override void Listen(int backlog)
        {
            SocketError bindResult = (SocketError)UnsafeMethods.listen(Handle, backlog);

            if (bindResult != SocketError.Success)
            {
                throw new SocketException();
            }
        }

        public override OperationResult Connect(IPEndPoint endPoint)
        {
            if (m_remoteAddress != null)
            {
                m_remoteAddress.Dispose();
                m_remoteAddress = null;
            }

            m_remoteAddress = new SocketAddress(endPoint.Address, endPoint.Port);

            int bytesSend;

            m_outOverlapped.OperationType = OperationType.Connect;

            if (m_connectEx(Handle, m_remoteAddress.Buffer, m_remoteAddress.Size, IntPtr.Zero, 0,
                out bytesSend, m_outOverlapped.Address))
            {
                return OperationResult.Completed;
            }
            else
            {
                SocketError socketError = (SocketError)Marshal.GetLastWin32Error();

                if (socketError != SocketError.IOPending)
                {
                    throw new SocketException((int)socketError);
                }

                return OperationResult.Pending;
            }
        }

        public override OperationResult Accept(OverlappedSocket socket)
        {
            if (m_acceptSocketBufferAddress == IntPtr.Zero)
            {
                m_acceptSocketBufferSize = (m_boundAddress.Size + 16) * 2;

                m_acceptSocketBufferAddress = Marshal.AllocHGlobal(m_acceptSocketBufferSize);
            }            

            int bytesReceived;

            var windowsSocket = socket as Windows.Socket;

            if (!m_acceptEx(Handle, windowsSocket.Handle, m_acceptSocketBufferAddress, 0,
                  m_acceptSocketBufferSize / 2,
                  m_acceptSocketBufferSize / 2, out bytesReceived, m_inOverlapped.Address))
            {
                var socketError = (SocketError)Marshal.GetLastWin32Error();

                if (socketError != SocketError.IOPending)
                {
                    throw new SocketException((int)socketError);
                }

                return OperationResult.Pending;
            }
            else
            {
                return OperationResult.Completed;
            }          
        }

        public override void BeginSend(AsyncIO.Buffer buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void BeginReceive(AsyncIO.Buffer buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
