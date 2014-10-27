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
    
    public interface ISocket : IDisposable
    {    
        AddressFamily AddressFamily { get; }

        SocketType SocketType { get; }

        ProtocolType ProtocolType { get; }

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

        void BindToCompletionPort(ICompletionPort completionPort);

        void Bind(IPEndPoint localEndPoint);

        void Listen(int backlog);

        OperationResult Connect(IPEndPoint endPoint);        
        
        void BeginAccept(ISocket socket);

        void BeginSend(IBuffer buffer, int offset, int count);

        void BeginReceive(IBuffer buffer, int offset, int count);
    }
}
