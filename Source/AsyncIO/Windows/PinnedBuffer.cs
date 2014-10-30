using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AsyncIO.Windows
{
    class PinnedBuffer : IDisposable
    {
        private GCHandle m_handle;
        
        public PinnedBuffer(byte[] buffer)
        {
            SetBuffer(buffer);
        }

        public byte[] Buffer { get; private set; }
        public IntPtr Address { get; private set; }

        public void Switch(byte[] buffer)
        {
            m_handle.Free();

            SetBuffer(buffer);
        }

        private void SetBuffer(byte[] buffer)
        {
            Buffer = buffer;
            m_handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Address = Marshal.UnsafeAddrOfPinnedArrayElement(Buffer, 0);
        }

        public void Dispose()
        {
            m_handle.Free();
            Buffer = null;
            Address = IntPtr.Zero;
        }
    }
}
