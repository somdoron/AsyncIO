using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AsyncIO.Windows
{
    internal class Socket : AsyncSocket
    {
        private Overlapped m_inOverlapped;
        private Overlapped m_outOverlapped;

        private ConnectExDelegate m_connectEx;
        private AcceptExDelegate m_acceptEx;
        private bool m_disposed;
        private SocketAddress m_boundAddress;
        private SocketAddress m_remoteAddress;

        private IntPtr m_acceptSocketBufferAddress;
        private int m_acceptSocketBufferSize;

        private PinnedBuffer m_sendPinnedBuffer;
        private PinnedBuffer m_receivePinnedBuffer;

        private WSABuffer m_sendWSABuffer;
        private WSABuffer m_receiveWSABuffer;

        private IPEndPoint m_localEndPoint;

        private Socket m_acceptSocket;

        public Socket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : base(addressFamily, socketType, protocolType)
        {
            m_disposed = false;

            m_inOverlapped = new Overlapped(this);
            m_outOverlapped = new Overlapped(this);

            m_sendWSABuffer = new WSABuffer();
            m_receiveWSABuffer = new WSABuffer();

            InitSocket();
            InitDynamicMethods();
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
                if (Environment.OSVersion.Version.Major == 5)
                    UnsafeMethods.CancelIo(Handle);
                else
                    UnsafeMethods.CancelIoEx(Handle, IntPtr.Zero);

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

                if (m_sendPinnedBuffer != null)
                {
                    m_sendPinnedBuffer.Dispose();
                    m_sendPinnedBuffer = null;
                }

                if (m_receivePinnedBuffer != null)
                {
                    m_receivePinnedBuffer.Dispose();
                    m_receivePinnedBuffer = null;
                }

                if (m_acceptSocketBufferAddress != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(m_acceptSocketBufferAddress);
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

        private void SetMulticastOption(SocketOptionName optionName, MulticastOption mr)
        {
            IPMulticastRequest mreq = new IPMulticastRequest();
            mreq.MulticastAddress = (int)mr.Group.Address;
            if (mr.LocalAddress != null)
            {
                mreq.InterfaceAddress = (int)mr.LocalAddress.Address;
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

            if (m_connectEx(Handle, m_remoteAddress.Buffer, m_remoteAddress.Size, IntPtr.Zero, 0,
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

        public override void Accept(AsyncSocket socket)
        {
            if (m_acceptSocketBufferAddress == IntPtr.Zero)
            {
                m_acceptSocketBufferSize = (m_boundAddress.Size + 16) * 2;

                m_acceptSocketBufferAddress = Marshal.AllocHGlobal(m_acceptSocketBufferSize);
            }

            int bytesReceived;

            m_acceptSocket = socket as Windows.Socket;

            m_inOverlapped.StartOperation(OperationType.Accept);

            if (!m_acceptEx(Handle, m_acceptSocket.Handle, m_acceptSocketBufferAddress, 0,
                  m_acceptSocketBufferSize / 2,
                  m_acceptSocketBufferSize / 2, out bytesReceived, m_inOverlapped.Address))
            {
                var socketError = (SocketError)Marshal.GetLastWin32Error();

                if (socketError != SocketError.IOPending)
                {
                    throw new SocketException((int)socketError);
                }                
            }
            else
            {                
                CompletionPort.PostCompletionStatus(m_inOverlapped.Address);
            }
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

            m_acceptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.UpdateAcceptContext, address);
            m_acceptSocket = null;
        }

        public override void Send(byte[] buffer, int offset, int count, SocketFlags flags)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            int bytesTransferred;

            if (m_sendPinnedBuffer == null)
            {
                m_sendPinnedBuffer = new PinnedBuffer(buffer);
            }
            else if (m_sendPinnedBuffer.Buffer != buffer)
            {
                m_sendPinnedBuffer.Switch(buffer);
            }

            m_outOverlapped.StartOperation(OperationType.Send);

            m_sendWSABuffer.Pointer = new IntPtr(m_sendPinnedBuffer.Address + offset);
            m_sendWSABuffer.Length = count;

            SocketError socketError = UnsafeMethods.WSASend(Handle, ref m_sendWSABuffer, 1,
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

            if (m_receivePinnedBuffer == null)
            {
                m_receivePinnedBuffer = new PinnedBuffer(buffer);
            }
            else if (m_receivePinnedBuffer.Buffer != buffer)
            {
                m_receivePinnedBuffer.Switch(buffer);
            }

            m_inOverlapped.StartOperation(OperationType.Receive);

            m_receiveWSABuffer.Pointer = new IntPtr(m_receivePinnedBuffer.Address + offset);
            m_receiveWSABuffer.Length = count;

            int bytesTransferred;

            SocketError socketError = UnsafeMethods.WSARecv(Handle, ref m_receiveWSABuffer, 1,
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

       
    }
}
