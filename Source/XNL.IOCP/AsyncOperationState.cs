using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace XNL.IOCP
{
  unsafe class AsyncOperationState : IDisposable
  {
    private readonly AsyncSocket m_socketExtended;
    private readonly Action<uint, uint> m_callback;                

    private GCHandle m_bufferGCHandle;
    private byte[] m_buffer = null;
    private int m_offset;
    private int m_count;

    private bool m_disposed = false;


    private NativeOverlapped* m_nativeOverlapped;

    

    public AsyncOperationState(AsyncSocket socketExtended, Action<uint, uint> callback)
    {
      m_socketExtended = socketExtended;
      m_callback = callback;

      m_nativeOverlapped = new Overlapped().UnsafePack(Complete, null);
            
      WSABuffer = new WSABuffer();
    }

    private void Complete(uint errorcode, uint numbytes, NativeOverlapped* poverlap)
    {
      m_callback(errorcode, numbytes);
    }    

    public WSABuffer WSABuffer;

    

    public IntPtr OverlappdAddress
    {
      get { return (IntPtr)((void*)m_nativeOverlapped); }
    }

    public void SetBuffer(byte[] buffer, int offset, int count)
    {
      if (!object.ReferenceEquals(m_buffer, buffer))
      {
        FreeBuffer();

        m_buffer = buffer;
        m_offset = offset;
        m_count = count;
        m_bufferGCHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        WSABuffer.Pointer = Marshal.UnsafeAddrOfPinnedArrayElement((Array) m_buffer, offset);
        WSABuffer.Length = count;
      }
      else
      {
        m_count = count;
        WSABuffer.Length = count;

        if (m_offset != offset)
        {
          WSABuffer.Pointer = Marshal.UnsafeAddrOfPinnedArrayElement((Array)m_buffer, offset);
          m_offset = offset;
        }
      }        
    }

    public void FreeBuffer()
    {
      if (m_buffer != null)
      {
        m_buffer = null;
        m_bufferGCHandle.Free();
        m_bufferGCHandle = new GCHandle();
      }
    }

    public void PrepareForCall()
    {
      
    }    

    public void Dispose()
    {
      if (!m_disposed)
      {
        m_disposed = true;
        FreeBuffer();
        
        Overlapped.Free(m_nativeOverlapped);
      }
    }
  }
}
