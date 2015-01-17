using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AsyncIO.DotNet;

namespace AsyncIO
{
    public enum OperationType
    {
        Send, Receive, Accept, Connect, Disconnect, Signal
    }

    public abstract class AsyncSocket : IDisposable
    {
        private SocketOptionName IPv6Only = (SocketOptionName)27;

        internal AsyncSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;
        }

        public static AsyncSocket Create(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            return new NativeSocket(addressFamily, socketType, protocolType);
        }

        public static AsyncSocket CreateIPv4Tcp()
        {
            return Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public static AsyncSocket CreateIPv6Tcp()
        {
            return Create(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
        }

        public AddressFamily AddressFamily { get; private set; }

        public SocketType SocketType { get; private set; }

        public ProtocolType ProtocolType { get; private set; }

        public abstract IPEndPoint LocalEndPoint { get; }

        public abstract IPEndPoint RemoteEndPoint { get; }

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
                    return (int)this.GetSocketOption(SocketOptionLevel.IPv6, IPv6Only) == 0;
            }
            set
            {
                if (this.AddressFamily != AddressFamily.InterNetworkV6)
                    throw new NotSupportedException("invalid version");
                this.SetSocketOption(SocketOptionLevel.IPv6, IPv6Only, value ? 0 : 1);
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
                if (AddressFamily == AddressFamily.InterNetwork)
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

        public abstract int IOControl(IOControlCode ioControlCode, byte[] optionInValue, byte[] optionOutValue);

        public abstract void Dispose();

        public abstract void Bind(IPEndPoint localEndPoint);

        public abstract void Listen(int backlog);

        public abstract void Connect(IPEndPoint endPoint);

        public abstract void Accept(AsyncSocket socket);

        public abstract void Send(byte[] buffer, int offset, int count, SocketFlags flags);

        public abstract void Receive(byte[] buffer, int offset, int count, SocketFlags flags);
    }
}
