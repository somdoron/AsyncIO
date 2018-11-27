using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AsyncIO.Windows
{
    internal sealed class Socket : AsyncSocket
    {
        private Overlapped m_inOverlapped;
        private Overlapped m_outOverlapped;

        private Connector m_connector;
        private Listener m_listener;
        private bool m_disposed;
        private SocketAddress m_boundAddress;
        private SocketAddress m_remoteAddress;

        public Socket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : base(addressFamily, socketType, protocolType)
        {
            m_disposed = false;

            m_inOverlapped = new Overlapped(this);
            m_outOverlapped = new Overlapped(this);

            InitSocket();
        }

        static Socket()
        {
            // we must initialize winsock, we create regualr .net socket for that
            using (var socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream,
                    ProtocolType.Tcp))
            {

            }
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

                // for Windows XP
#if NETSTANDARD1_3
                UnsafeMethods.CancelIoEx(Handle, IntPtr.Zero);
#else
                if (Environment.OSVersion.Version.Major == 5)
                    UnsafeMethods.CancelIo(Handle);
                else
                    UnsafeMethods.CancelIoEx(Handle, IntPtr.Zero);
#endif

                int error = UnsafeMethods.closesocket(Handle);

                if (error != 0)
                {
                    error = Marshal.GetLastWin32Error();
                }


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

                if (m_listener != null)
                {
                    m_listener.Dispose();
                }
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IntPtr Handle { get; private set; }

        public CompletionPort CompletionPort { get; private set; }

        public override IPEndPoint RemoteEndPoint
        {
            get
            {
                using ( var socketAddress = new SocketAddress( AddressFamily, AddressFamily == AddressFamily.InterNetwork ? 16 : 28))
                {
                    int size = socketAddress.Size;

                    if (UnsafeMethods.getpeername(Handle, socketAddress.Buffer, ref size) != SocketError.Success)
                    {
                        throw new SocketException();
                    }
                    
                    return socketAddress.GetEndPoint();
                }
            }
        }

        public override IPEndPoint LocalEndPoint
        {
            get
            {
                using (var  socketAddress = new SocketAddress(AddressFamily, AddressFamily == AddressFamily.InterNetwork ? 16 : 28))
                { 
                    int size = socketAddress.Size;

                    if (UnsafeMethods.getsockname(Handle, socketAddress.Buffer, ref size) != SocketError.Success)
                    {
                        throw new SocketException();
                    }
                    
                    return socketAddress.GetEndPoint();
                }
            }
        }

        private void InitSocket()
        {
            Handle = UnsafeMethods.WSASocket(AddressFamily, SocketType, ProtocolType,
                IntPtr.Zero, 0, SocketConstructorFlags.WSA_FLAG_OVERLAPPED);

            if (Handle == UnsafeMethods.INVALID_HANDLE_VALUE)
            {
                throw new SocketException();
            }
        }

#if NETSTANDARD1_6
        private T LoadDynamicMethod<T>(Guid guid)
#else
        private Delegate LoadDynamicMethod<T>(Guid guid)
#endif
        {
            IntPtr connectExAddress = IntPtr.Zero;
            int byteTransfered = 0;

            SocketError socketError = (SocketError)UnsafeMethods.WSAIoctl(Handle, UnsafeMethods.GetExtensionFunctionPointer,
                ref guid, Marshal.SizeOf(guid), ref connectExAddress, IntPtr.Size, ref byteTransfered, IntPtr.Zero, IntPtr.Zero);

            if (socketError != SocketError.Success)
            {
                throw new SocketException();
            }

#if NETSTANDARD1_6
            return Marshal.GetDelegateForFunctionPointer<T>(connectExAddress);
#else
            return Marshal.GetDelegateForFunctionPointer(connectExAddress, typeof(T));
#endif
        }

        internal void SetCompletionPort(CompletionPort completionPort, object state)
        {                       
            CompletionPort = completionPort;
            m_inOverlapped.State = state;
            m_outOverlapped.State = state;
        }

        public override void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
        {
            if (UnsafeMethods.setsockopt(Handle, optionLevel, optionName, optionValue, optionValue != null ? optionValue.Length : 0) == SocketError.SocketError)
            {
                throw new SocketException();
            }
        }

        public override void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue)
        {
            if (UnsafeMethods.setsockopt(Handle, optionLevel, optionName, ref optionValue, 4) == SocketError.SocketError)
            {
                throw new SocketException();
            }
        }

        public override void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue)
        {
            SetSocketOption(optionLevel, optionName, optionValue ? 1 : 0);
        }

        public override void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue)
        {
            if (optionValue == null)
                throw new ArgumentNullException("optionValue");

            if (optionLevel == SocketOptionLevel.Socket && optionName == SocketOptionName.Linger)
            {
                LingerOption lref = optionValue as LingerOption;
                if (lref == null)
                    throw new ArgumentException("invalid option value", "optionValue");
                else if (lref.LingerTime < 0 || lref.LingerTime > (int)ushort.MaxValue)
                    throw new ArgumentOutOfRangeException("optionValue.LingerTime");
                else
                    this.SetLingerOption(lref);
            }
            else if (optionLevel == SocketOptionLevel.IP && (optionName == SocketOptionName.AddMembership || optionName == SocketOptionName.DropMembership))
            {
                MulticastOption MR = optionValue as MulticastOption;
                if (MR == null)
                    throw new ArgumentException("optionValue");
                else
                    this.SetMulticastOption(optionName, MR);
            }
            else
            {
                if (optionLevel != SocketOptionLevel.IPv6 ||
                    optionName != SocketOptionName.AddMembership &&
                    optionName != SocketOptionName.DropMembership)
                    throw new ArgumentException("optionValue");
                IPv6MulticastOption MR = optionValue as IPv6MulticastOption;
                if (MR == null)
                    throw new ArgumentException("optionValue");
                else
                    this.SetIPv6MulticastOption(optionName, MR);
            }
        }

        private void SetIPv6MulticastOption(SocketOptionName optionName, IPv6MulticastOption mr)
        {
            var optionValue = new IPv6MulticastRequest()
            {
                MulticastAddress = mr.Group.GetAddressBytes(),
                InterfaceIndex = (int)mr.InterfaceIndex
            };

            if (UnsafeMethods.setsockopt(Handle, SocketOptionLevel.IPv6, optionName, ref optionValue, IPv6MulticastRequest.Size) ==
                SocketError.SocketError)
            {
                throw new SocketException();
            }
        }

        private int GetIP4Address(IPAddress ipAddress)
        {
            var bytes = ipAddress.GetAddressBytes();
            return bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24;            
        }

        private void SetMulticastOption(SocketOptionName optionName, MulticastOption mr)
        {
            IPMulticastRequest mreq = new IPMulticastRequest();
            mreq.MulticastAddress = GetIP4Address(mr.Group);
            if (mr.LocalAddress != null)
            {
                mreq.InterfaceAddress = GetIP4Address(mr.LocalAddress);
            }
            else
            {
                int num = IPAddress.HostToNetworkOrder(mr.InterfaceIndex);
                mreq.InterfaceAddress = num;
            }

            if (UnsafeMethods.setsockopt(Handle, SocketOptionLevel.IPv6, optionName, ref mreq, IPv6MulticastRequest.Size) ==
               SocketError.SocketError)
            {
                throw new SocketException();
            }
        }

        private void SetLingerOption(LingerOption lref)
        {
            var optionValue = new Linger()
            {
                OnOff = lref.Enabled ? (ushort)1 : (ushort)0,
                Time = (ushort)lref.LingerTime
            };

            if (UnsafeMethods.setsockopt(Handle, SocketOptionLevel.Socket, SocketOptionName.Linger, ref optionValue, 4) ==
                SocketError.SocketError)
            {
                throw new SocketException();
            }
        }

        public override void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
        {
            int optionLength = optionValue != null ? optionValue.Length : 0;
            if (UnsafeMethods.getsockopt(this.Handle, optionLevel, optionName, optionValue, ref optionLength) == SocketError.SocketError)
            {
                throw new SocketException();
            }
        }

        public override byte[] GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionLength)
        {
            byte[] optionValue = new byte[optionLength];
            int optionLength1 = optionLength;
            if (UnsafeMethods.getsockopt(Handle, optionLevel, optionName, optionValue, ref optionLength1) != SocketError.SocketError)
            {
                if (optionLength != optionLength1)
                {
                    byte[] numArray = new byte[optionLength1];
                    Buffer.BlockCopy(optionValue, 0, numArray, 0, optionLength1);
                    optionValue = numArray;
                }
                return optionValue;
            }
            else
            {
                throw new SocketException();
            }
        }

        public override object GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName)
        {
            if (optionLevel == SocketOptionLevel.Socket && optionName == SocketOptionName.Linger)
                return (object)this.GetLingerOpt();
            if (optionLevel == SocketOptionLevel.IP && (optionName == SocketOptionName.AddMembership || optionName == SocketOptionName.DropMembership))
                return (object)this.GetMulticastOpt(optionName);
            if (optionLevel == SocketOptionLevel.IPv6 && (optionName == SocketOptionName.AddMembership || optionName == SocketOptionName.DropMembership))
                return (object)this.GetIPv6MulticastOpt(optionName);

            int optionValue = 0;
            int optionLength = 4;

            if (UnsafeMethods.getsockopt(Handle, optionLevel, optionName, out optionValue, ref optionLength) != SocketError.SocketError)
            {
                return optionValue;
            }
            else
            {
                throw new SocketException();
            }
        }

        private object GetIPv6MulticastOpt(SocketOptionName optionName)
        {
            throw new NotImplementedException();
        }

        private object GetMulticastOpt(SocketOptionName optionName)
        {
            throw new NotImplementedException();
        }

        private object GetLingerOpt()
        {
            throw new NotImplementedException();
        }

        public override int IOControl(IOControlCode ioControlCode, byte[] optionInValue, byte[] optionOutValue)
        {
            int bytesTransferred = 0;

            if (UnsafeMethods.WSAIoctl_Blocking(Handle, (int) ioControlCode, optionInValue,
                optionInValue != null ? optionInValue.Length : 0, optionOutValue,
                optionOutValue != null ? optionOutValue.Length : 0, out bytesTransferred, IntPtr.Zero, IntPtr.Zero) !=
                SocketError.SocketError)
            {
                return bytesTransferred;
            }

            throw new SocketException();
        }

        public override void Bind(IPEndPoint localEndPoint)
        {
            if (m_boundAddress != null)
            {
                m_boundAddress.Dispose();
                m_boundAddress = null;
            }

            m_boundAddress = new SocketAddress(localEndPoint.Address, localEndPoint.Port);

            // Accoring MSDN bind returns 0 if succeeded
            // and SOCKET_ERROR otherwise
            if (0 != UnsafeMethods.bind(Handle, m_boundAddress.Buffer, m_boundAddress.Size))
            {
                throw new SocketException();
            }
        }

        public override void Listen(int backlog)
        {
            // Accoring MSDN listen returns 0 if succeeded
            // and SOCKET_ERROR otherwise
            if (0 != UnsafeMethods.listen(Handle, backlog))
            {
                throw new SocketException();
            }
            m_listener = new Listener(this);
        }

        public override void Connect(IPEndPoint endPoint)
        {
            if (m_remoteAddress != null)
            {
                m_remoteAddress.Dispose();
                m_remoteAddress = null;
            }

            m_remoteAddress = new SocketAddress(endPoint.Address, endPoint.Port);

            if (m_boundAddress == null)
            {
                if (endPoint.AddressFamily == AddressFamily.InterNetwork)
                    Bind(new IPEndPoint(IPAddress.Any, 0));
                else
                    Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
            }

            int bytesSend;

            m_outOverlapped.StartOperation(OperationType.Connect);

            if ((m_connector ?? (m_connector = new Connector(this))).m_connectEx(Handle, m_remoteAddress.Buffer, m_remoteAddress.Size, IntPtr.Zero, 0,
                out bytesSend, m_outOverlapped.Address))
            {                
                CompletionPort.PostCompletionStatus(m_outOverlapped.Address);
            }
            else
            {
                SocketError socketError = (SocketError)Marshal.GetLastWin32Error();

                if (socketError != SocketError.IOPending)
                {
                    throw new SocketException((int)socketError);
                }                
            }
        }

        internal void UpdateConnect()
        {
            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.UpdateConnectContext, null);
        }

        public override AsyncSocket GetAcceptedSocket()
        {
            return m_listener.GetAcceptedSocket();
        }

        public override void Accept()
        {
            m_listener.AcceptInternal(new Socket(this.AddressFamily, this.SocketType, this.ProtocolType));
        }

        public override void Accept(AsyncSocket socket)
        {
            m_listener.AcceptInternal(socket);
        }

        internal void UpdateAccept()
        {
            Byte[] address;

            if (IntPtr.Size == 4)
            {
                address = BitConverter.GetBytes(Handle.ToInt32());
            }
            else
            {
                address = BitConverter.GetBytes(Handle.ToInt64());
            }

            m_listener.m_acceptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.UpdateAcceptContext, address);            
        }

        public override void Send(byte[] buffer, int offset, int count, SocketFlags flags)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            int bytesTransferred;

            m_outOverlapped.StartOperation(OperationType.Send, buffer);

            var sendBuffer = new WSABuffer
            {
                Pointer = new IntPtr(m_outOverlapped.BufferAddress + offset),
                Length = count
            };

            SocketError socketError = UnsafeMethods.WSASend(Handle, ref sendBuffer, 1,
              out bytesTransferred, flags, m_outOverlapped.Address, IntPtr.Zero);

            if (socketError != SocketError.Success)
            {
                socketError = (SocketError)Marshal.GetLastWin32Error();

                if (socketError != SocketError.IOPending)
                {                
                    throw new SocketException((int)socketError);
                }
            }            
        }

        public override void Receive(byte[] buffer, int offset, int count, SocketFlags flags)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            m_inOverlapped.StartOperation(OperationType.Receive, buffer);

            var receiveBuffer = new WSABuffer();
            receiveBuffer.Pointer = new IntPtr(m_inOverlapped.BufferAddress + offset);
            receiveBuffer.Length = count;

            int bytesTransferred;

            SocketError socketError = UnsafeMethods.WSARecv(Handle, ref receiveBuffer, 1,
              out bytesTransferred, ref flags, m_inOverlapped.Address, IntPtr.Zero);

            if (socketError != SocketError.Success)
            {
                socketError = (SocketError)Marshal.GetLastWin32Error();

                if (socketError != SocketError.IOPending)
                {
                    throw new SocketException((int)socketError);
                }
            }          
        }

        sealed class Connector
        {
            internal readonly ConnectExDelegate m_connectEx;

            public Connector(Socket socket)
            {
                m_connectEx =
                    (ConnectExDelegate)socket.LoadDynamicMethod<ConnectExDelegate>(UnsafeMethods.WSAID_CONNECTEX);
            }
        }

        sealed class Listener : IDisposable
        {
            private readonly Socket m_socket;
            internal readonly AcceptExDelegate m_acceptEx;
            private IntPtr m_acceptSocketBufferAddress;
            private int m_acceptSocketBufferSize;
            internal Socket m_acceptSocket;

            public Listener(Socket socket)
            {
                m_socket = socket;
                m_acceptEx =
                    (AcceptExDelegate)m_socket.LoadDynamicMethod<AcceptExDelegate>(UnsafeMethods.WSAID_ACCEPT_EX);
            }
            ~Listener()
            {
                Dispose(false);
            }
            public Socket GetAcceptedSocket()
            {
                return Interlocked.Exchange(ref m_acceptSocket, null);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            public void AcceptInternal(AsyncSocket socket)
            {
                if (m_acceptSocketBufferAddress == IntPtr.Zero)
                {
                    m_acceptSocketBufferSize = m_socket.m_boundAddress.Size + 16;

                    m_acceptSocketBufferAddress = Marshal.AllocHGlobal(m_acceptSocketBufferSize << 1);
                }

                int bytesReceived;

                m_acceptSocket = socket as Windows.Socket;

                m_socket.m_inOverlapped.StartOperation(OperationType.Accept);

                if (!m_acceptEx(m_socket.Handle, m_acceptSocket.Handle, m_acceptSocketBufferAddress, 0,
                      m_acceptSocketBufferSize,
                      m_acceptSocketBufferSize, out bytesReceived, m_socket.m_inOverlapped.Address))
                {
                    var socketError = (SocketError)Marshal.GetLastWin32Error();

                    if (socketError != SocketError.IOPending)
                    {
                        throw new SocketException((int)socketError);
                    }
                }
                else
                {
                    m_socket.CompletionPort.PostCompletionStatus(m_socket.m_inOverlapped.Address);
                }
            }

            void Dispose(bool disposing)
            {
                if (m_acceptSocketBufferAddress != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(m_acceptSocketBufferAddress);
                    m_acceptSocketBufferAddress = IntPtr.Zero;
                }

                if (m_acceptSocket != null)
                {
                    m_acceptSocket.Dispose(disposing);
                    m_acceptSocket = null;
                }
            }
        }
    }
}
