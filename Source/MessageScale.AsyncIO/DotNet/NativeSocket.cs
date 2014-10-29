using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace MessageScale.AsyncIO.DotNet
{
    class NativeSocket : OverlappedSocket
    {
        private Socket m_socket;

        private CompletionPort m_completionPort;
        private object m_state;

        private SocketAsyncEventArgs m_inSocketAsyncEventArgs;
        private SocketAsyncEventArgs m_outSocketAsyncEventArgs;

        public NativeSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : base(addressFamily, socketType, protocolType)
        {
            m_socket = new Socket(addressFamily, socketType, protocolType);

            m_inSocketAsyncEventArgs = new SocketAsyncEventArgs();
            m_inSocketAsyncEventArgs.Completed += OnAsyncCompleted;

            m_outSocketAsyncEventArgs = new SocketAsyncEventArgs();
            m_outSocketAsyncEventArgs.Completed += OnAsyncCompleted;
        }

        private void OnAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            OperationType operationType;

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    operationType = OperationType.Accept;
                    break;
                case SocketAsyncOperation.Connect:
                    operationType = OperationType.Connect;
                    break;
                case SocketAsyncOperation.Receive:
                    operationType = OperationType.Receive;
                    break;
                case SocketAsyncOperation.Send:
                    operationType = OperationType.Send;
                    break;
                case SocketAsyncOperation.Disconnect:
                    operationType = OperationType.Disconnect;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            CompletionStatus completionStatus = new CompletionStatus(this, m_state, operationType, e.SocketError,
                e.BytesTransferred);

            m_completionPort.Queue(ref completionStatus);
        }

        internal void SetCompletionPort(CompletionPort completionPort, object state)
        {
            m_completionPort = completionPort;
            m_state = state;
        }

        public override void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue)
        {
            m_socket.SetSocketOption(optionLevel, optionName, optionValue);
        }

        public override void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
        {
            m_socket.SetSocketOption(optionLevel, optionName, optionValue);
        }

        public override void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue)
        {
            m_socket.SetSocketOption(optionLevel, optionName, optionValue);
        }

        public override void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue)
        {
            m_socket.SetSocketOption(optionLevel, optionName, optionValue);
        }

        public override object GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName)
        {
            return m_socket.GetSocketOption(optionLevel, optionName);
        }

        public override void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
        {
            m_socket.GetSocketOption(optionLevel, optionName, optionValue);
        }

        public override byte[] GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionLength)
        {
            return m_socket.GetSocketOption(optionLevel, optionName, optionLength);
        }

        public override void Dispose()
        {
            m_socket.Dispose();
        }

        public override void Bind(System.Net.IPEndPoint localEndPoint)
        {
            m_socket.Bind(localEndPoint);
        }

        public override void Listen(int backlog)
        {
            m_socket.Listen(backlog);
        }

        public override OperationResult Connect(System.Net.IPEndPoint endPoint)
        {
            m_outSocketAsyncEventArgs.RemoteEndPoint = endPoint;

            if (m_socket.ConnectAsync(m_outSocketAsyncEventArgs))
            {
                return OperationResult.Pending;
            }
            else
            {
                return OperationResult.Completed;
            }
        }

        public override OperationResult Accept(OverlappedSocket socket)
        {
            NativeSocket nativeSocket = (NativeSocket)socket;

            m_inSocketAsyncEventArgs.AcceptSocket = nativeSocket.m_socket;

            if (m_socket.AcceptAsync(m_inSocketAsyncEventArgs))
            {
                return OperationResult.Pending;
            }
            else
            {
                return OperationResult.Completed;
            }
        }

        public override OperationResult Send(byte[] buffer, int offset, int count, SocketFlags flags)
        {                        
            if (m_outSocketAsyncEventArgs.Buffer != buffer)
            {
                m_outSocketAsyncEventArgs.SetBuffer(buffer, offset, count);
            }
            else if (m_outSocketAsyncEventArgs.Offset != offset || m_inSocketAsyncEventArgs.Count != count)
            {
                m_outSocketAsyncEventArgs.SetBuffer(offset, count);
            }

            if (m_socket.SendAsync(m_outSocketAsyncEventArgs))
            {
                return OperationResult.Pending;
            }
            else
            {
                return OperationResult.Completed;
            }
        }

        public override OperationResult Receive(byte[] buffer, int offset, int count, SocketFlags flags, out int bytesTransferred)
        {
            m_inSocketAsyncEventArgs.AcceptSocket = null;

            if (m_inSocketAsyncEventArgs.Buffer != buffer)
            {
                m_inSocketAsyncEventArgs.SetBuffer(buffer, offset, count);
            }
            else if (m_inSocketAsyncEventArgs.Offset != offset || m_inSocketAsyncEventArgs.Count != count)
            {
                m_inSocketAsyncEventArgs.SetBuffer(offset, count);
            }

            if (m_socket.ReceiveAsync(m_inSocketAsyncEventArgs))
            {
                bytesTransferred = 0;
                return OperationResult.Pending;
            }
            else
            {
                bytesTransferred = m_inSocketAsyncEventArgs.BytesTransferred;
                return OperationResult.Completed;
            }
        }
    }
}
