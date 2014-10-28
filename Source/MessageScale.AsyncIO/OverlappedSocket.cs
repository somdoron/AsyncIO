using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MessageScale.AsyncIO
{
    public enum OperationType
    {
        Send, Receive, Accept, Connect, Disconnect, Signal
    }

    public enum OperationResult
    {
        Completed, Pending
    }
    
    public abstract class OverlappedSocket : IDisposable
    {
        internal OverlappedSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;
        }

        public static OverlappedSocket Create(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            return new MessageScale.AsyncIO.Windows.Socket(addressFamily, socketType, protocolType);
        }

        public AddressFamily AddressFamily { get; private set; }

        public SocketType SocketType { get; private set; }

        public ProtocolType ProtocolType { get; private set; }

        //bool IsBound { get; }

        //bool NoDelay { get; set; }

        //bool ExclusiveAddressUse { get; set; }

        //bool DualMode { get; set; }

        //int ReceiveBufferSize { get; set; }

        //int SendBufferSize { get; set; }

        //int ReceiveTimeout { get; set; }

        //int SendTimeout { get; set; }        

        //void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue);

        //void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue);

        //void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue);

        //void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, Object optionValue);

        //Object GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName);

        //void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue);

        //byte[] GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionLength);

        public abstract void Dispose();
         
        public abstract void Bind(IPEndPoint localEndPoint);

        public abstract void Listen(int backlog);

        public abstract OperationResult Connect(IPEndPoint endPoint);

        public abstract OperationResult Accept(OverlappedSocket socket);

        public abstract void BeginSend(Buffer buffer, int offset, int count);

        public abstract void BeginReceive(Buffer buffer, int offset, int count);
    }
}
