using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace AsyncIO.Windows
{
  class SocketAddress : IDisposable
  {
    private byte[] m_buffer;
    private AddressFamily m_addressFamily;

    private bool m_disposed = false;
    private GCHandle m_bufferHandle;
    private IntPtr m_bufferAddress;

    public SocketAddress(AddressFamily addressFamily, int size)
    {
      Size = size;
      m_addressFamily = addressFamily;
      m_buffer = new byte[size];

      m_buffer = new byte[(size / IntPtr.Size + 2) * IntPtr.Size];
      m_buffer[0] = (byte)addressFamily;
      m_buffer[1] = (byte)((uint)addressFamily >> 8);

      m_bufferHandle = GCHandle.Alloc(m_buffer, GCHandleType.Pinned);
      m_bufferAddress = Marshal.UnsafeAddrOfPinnedArrayElement(m_buffer, 0);
    }

    public SocketAddress(IPAddress ipAddress)
      : this(ipAddress.AddressFamily, ipAddress.AddressFamily == AddressFamily.InterNetwork ? 16 : 28)
    {
      this.m_buffer[2] = (byte)0;
      this.m_buffer[3] = (byte)0;
      if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
      {
        this.m_buffer[4] = (byte)0;
        this.m_buffer[5] = (byte)0;
        this.m_buffer[6] = (byte)0;
        this.m_buffer[7] = (byte)0;
        long scopeId = ipAddress.ScopeId;
        this.m_buffer[24] = (byte)scopeId;
        this.m_buffer[25] = (byte)(scopeId >> 8);
        this.m_buffer[26] = (byte)(scopeId >> 16);
        this.m_buffer[27] = (byte)(scopeId >> 24);
        byte[] addressBytes = ipAddress.GetAddressBytes();
        for (int index = 0; index < addressBytes.Length; ++index)
          this.m_buffer[8 + index] = addressBytes[index];
      }
      else
      {
        System.Buffer.BlockCopy(ipAddress.GetAddressBytes(), 0, m_buffer, 4, 4);
      }
    }

    public IPEndPoint GetEndPoint()
    {
      return new IPEndPoint(GetIPAddress(), (int)Buffer[2] << 8 & 65280 | (int)Buffer[3]);
    }

    internal IPAddress GetIPAddress()
    {
      if (m_addressFamily == AddressFamily.InterNetworkV6)
      {
        byte[] address = new byte[16];
        for (int index = 0; index < address.Length; ++index)
          address[index] = this.Buffer[index + 8];
        long scopeid = (long)(((int)this.Buffer[27] << 24) + ((int)this.Buffer[26] << 16) + ((int)this.Buffer[25] << 8) + (int)this.Buffer[24]);
        return new IPAddress(address, scopeid);
      }
      else if (m_addressFamily == AddressFamily.InterNetwork)
        return new IPAddress((long)((int)this.Buffer[4] & (int)byte.MaxValue | (int)this.Buffer[5] << 8 & 65280 | (int)this.Buffer[6] << 16 & 16711680 | (int)this.Buffer[7] << 24) & (long)uint.MaxValue);
      else
        throw new SocketException((int)SocketError.AddressFamilyNotSupported);
    }

    public SocketAddress(IPAddress ipAddress, int port)
      : this(ipAddress)
    {
      this.m_buffer[2] = (byte)(port >> 8);
      this.m_buffer[3] = (byte)port;
    }

    public int Size { get; private set; }

    public IntPtr PinnedAddressBuffer
    {
      get { return m_bufferAddress; }
    }

    public void Dispose()
    {
      if (!m_disposed)
      {
        m_disposed = true;
        m_bufferHandle.Free();
      }
    }

    public byte[] Buffer { get { return m_buffer; } }
  }
}
