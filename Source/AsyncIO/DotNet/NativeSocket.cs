using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AsyncIO.DotNet
{
    class NativeSocket : AsyncSocket
    {
        private Socket m_socket;

        private CompletionPort m_completionPort;
        private object m_state;

        private SocketAsyncEventArgs m_inSocketAsyncEventArgs;
        private SocketAsyncEventArgs m_outSocketAsyncEventArgs;
        private NativeSocket m_acceptedSocket;

        public NativeSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : base(addressFamily, socketType, protocolType)
        {
            m_socket = new Socket(addressFamily, socketType, protocolType);

            m_inSocketAsyncEventArgs = new SocketAsyncEventArgs();
            m_inSocketAsyncEventArgs.Completed += OnAsyncCompleted;

            m_outSocketAsyncEventArgs = new SocketAsyncEventArgs();
            m_outSocketAsyncEventArgs.Completed += OnAsyncCompleted;
        }

        private NativeSocket(Socket socket) : base(socket.AddressFamily, socket.SocketType, socket.ProtocolType)
        {
            m_socket = socket;

            m_inSocketAsyncEventArgs = new SocketAsyncEventArgs();
            m_inSocketAsyncEventArgs.Completed += OnAsyncCompleted;

            m_outSocketAsyncEventArgs = new SocketAsyncEventArgs();
            m_outSocketAsyncEventArgs.Completed += OnAsyncCompleted;
        }

        public override System.Net.IPEndPoint LocalEndPoint
        {
            get { return (IPEndPoint)m_socket.LocalEndPoint; }
        }

        public override IPEndPoint RemoteEndPoint
        {
            get { return (IPEndPoint)m_socket.RemoteEndPoint; }
        }

        private void OnAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            OperationType operationType;

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    operationType = OperationType.Accept;
                    if (e.SocketError == SocketError.Success)
                        m_acceptedSocket = new NativeSocket(e.AcceptSocket);
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
    
        public override int IOControl(IOControlCode ioControlCode, byte[] optionInValue, byte[] optionOutValue)
        {
            return m_socket.IOControl(ioControlCode, optionInValue, optionOutValue);
        }

        public override void Dispose()
        {
            (m_socket as IDisposable).Dispose();
            m_inSocketAsyncEventArgs.Dispose();
            m_outSocketAsyncEventArgs.Dispose();

            if (m_acceptedSocket != null)
            {
                m_acceptedSocket.Dispose();
            }
        }

        public override void Bind(System.Net.IPEndPoint localEndPoint)
        {
            m_socket.Bind(localEndPoint);
        }

        public override void Listen(int backlog)
        {
            m_socket.Listen(backlog);
        }

        public override void Connect(System.Net.IPEndPoint endPoint)
        {
            m_outSocketAsyncEventArgs.RemoteEndPoint = endPoint;

            if (!m_socket.ConnectAsync(m_outSocketAsyncEventArgs))
            {
                CompletionStatus completionStatus = new CompletionStatus(this, m_state, OperationType.Connect, m_outSocketAsyncEventArgs.SocketError, 0);

		// Mono have an issue, when connect operation fail the SocketAsyncEventArgs is kept in progress. We therefore dispose the exising one and creating a new one.
                m_outSocketAsyncEventArgs.Completed -= OnAsyncCompleted;
		try {
		    m_outSocketAsyncEventArgs.Dispose();
		}
		catch (Exception ex) {
		    			
		}
		
		m_outSocketAsyncEventArgs = new SocketAsyncEventArgs();
                m_outSocketAsyncEventArgs.Completed += OnAsyncCompleted;

                m_completionPort.Queue(ref completionStatus);
            }
        }

        public override void Accept()
        {
            m_inSocketAsyncEventArgs.AcceptSocket = null;

            if (!m_socket.AcceptAsync(m_inSocketAsyncEventArgs))
            {
                if (m_inSocketAsyncEventArgs.SocketError == SocketError.Success)
                    m_acceptedSocket = new NativeSocket(m_inSocketAsyncEventArgs.AcceptSocket);

                CompletionStatus completionStatus = new CompletionStatus(this, m_state, OperationType.Accept, SocketError.Success, 0);
                
                m_completionPort.Queue(ref completionStatus);
            }
        }

        public override AsyncSocket GetAcceptedSocket()
        {
            m_inSocketAsyncEventArgs.AcceptSocket = null;
            var temp = m_acceptedSocket;
            m_acceptedSocket = null;
            return temp;
        }

        [Obsolete("Use Accept without parameter and GetAcceptedSocket")]
        public override void Accept(AsyncSocket socket)
        {
            NativeSocket nativeSocket = (NativeSocket)socket;

            m_inSocketAsyncEventArgs.AcceptSocket = nativeSocket.m_socket;

            if (!m_socket.AcceptAsync(m_inSocketAsyncEventArgs))
            {
                CompletionStatus completionStatus = new CompletionStatus(this, m_state, OperationType.Accept, SocketError.Success, 0);

                m_completionPort.Queue(ref completionStatus);
            }
        }

        public override void Send(byte[] buffer, int offset, int count, SocketFlags flags)
        {
            if (m_outSocketAsyncEventArgs.Buffer != buffer)
            {
                m_outSocketAsyncEventArgs.SetBuffer(buffer, offset, count);
            }
            else if (m_outSocketAsyncEventArgs.Offset != offset || m_inSocketAsyncEventArgs.Count != count)
            {
                m_outSocketAsyncEventArgs.SetBuffer(offset, count);
            }

            if (!m_socket.SendAsync(m_outSocketAsyncEventArgs))
            {
                CompletionStatus completionStatus = new CompletionStatus(this, m_state, OperationType.Send, m_outSocketAsyncEventArgs.SocketError,
                    m_outSocketAsyncEventArgs.BytesTransferred);

                m_completionPort.Queue(ref completionStatus);
            }
        }

        public override void Receive(byte[] buffer, int offset, int count, SocketFlags flags)
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

            if (!m_socket.ReceiveAsync(m_inSocketAsyncEventArgs))
            {
                CompletionStatus completionStatus = new CompletionStatus(this, m_state, OperationType.Receive, m_inSocketAsyncEventArgs.SocketError,
                    m_inSocketAsyncEventArgs.BytesTransferred);

                m_completionPort.Queue(ref completionStatus);
            }
        }


    }
}
