using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using MessageScale.AsyncIO.DotNet;

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
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || ForceDotNet.Forced)
            {
                return new NativeSocket(addressFamily, socketType, protocolType);
            }
            else
            {
                return new Windows.Socket(addressFamily, socketType, protocolType);                    
            }            
        }

        public AddressFamily AddressFamily { get; private set; }

        public SocketType SocketType { get; private set; }

        public ProtocolType ProtocolType { get; private set; }        

        public bool NoDelay
        {
            get
            {
                return (int)this.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay) != 0;
            }
            set
            {
                this.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, value ? 1 : 0);
            }
        }

        public bool ExclusiveAddressUse
        {
            get
            {
                return (int)this.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse) != 0;
            }
            set
            {                
                this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, value ? 1 : 0);
            }
        }

        public bool DualMode
        {
            get
            {
                if (this.AddressFamily != AddressFamily.InterNetworkV6)
                    throw new NotSupportedException("invalid version");
                else
                    return (int)this.GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only) == 0;
            }
            set
            {
                if (this.AddressFamily != AddressFamily.InterNetworkV6)
                    throw new NotSupportedException("invalid version");
                this.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, value ? 0 : 1);
            }
        }

        public int ReceiveBufferSize
        {
            get
            {
                return (int)this.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer);
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");
                this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, value);
            }
        }

        public int SendBufferSize
        {
            get
            {
                return (int)this.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer);
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");
                this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, value);
            }
        }

        public LingerOption LingerState
        {
            get
            {
                return (LingerOption)this.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger);
            }            
            set
            {
                this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, (object)value);
            }
        }

        public bool EnableBroadcast
        {
            get
            {
                return (int)this.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast) != 0;
            }
            set
            {
                this.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, value ? 1 : 0);
            }
        }

        public bool MulticastLoopback
        {
            get
            {
                if (AddressFamily == AddressFamily.InterNetwork)
                {
                    return (int)this.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback) != 0;
                }
                else
                {
                    if (AddressFamily != AddressFamily.InterNetworkV6)
                        throw new NotSupportedException("invalid version");
                    return (int)this.GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback) != 0;
                }
            }
            set
            {
                if (AddressFamily == AddressFamily.InterNetwork)
                {
                    this.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, value ? 1 : 0);
                }
                else
                {
                    if (AddressFamily != AddressFamily.InterNetworkV6)
                        throw new NotSupportedException("invalid version");            
                    this.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, value ? 1 : 0);
                }
            }
        }

        public short Ttl
        {
            get
            {
                if (AddressFamily  == AddressFamily.InterNetwork)
                    return (short)(int)this.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress);
                if (AddressFamily == AddressFamily.InterNetworkV6)
                    return (short)(int)this.GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.ReuseAddress);
                else
                    throw new NotSupportedException("invalid version");    
            }
            set
            {
                if ((int)value < 0 || (int)value > (int)byte.MaxValue)
                    throw new ArgumentOutOfRangeException("value");
                if (AddressFamily == AddressFamily.InterNetwork)
                {
                    this.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, (int)value);
                }
                else
                {
                    if (AddressFamily != AddressFamily.InterNetworkV6)
                        throw new NotSupportedException("invalid version");    
                    this.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.ReuseAddress, (int)value);
                }
            }
        }

        public abstract void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue);
        public abstract void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue);
        public abstract void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, Object optionValue);
        public abstract void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue);

        public abstract Object GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName);
        public abstract void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue);
        public abstract byte[] GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionLength);

        public abstract void Dispose();
         
        public abstract void Bind(IPEndPoint localEndPoint);

        public abstract void Listen(int backlog);

        public abstract OperationResult Connect(IPEndPoint endPoint);

        public abstract OperationResult Accept(OverlappedSocket socket);

        public abstract OperationResult Send(byte[] buffer, int offset, int count, SocketFlags flags);

        public abstract OperationResult Receive(byte[] buffer, int offset, int count, SocketFlags flags, out int bytesTransferred);
    }
}
