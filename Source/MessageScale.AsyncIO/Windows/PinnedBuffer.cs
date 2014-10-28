using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MessageScale.AsyncIO.Windows
{
    public class PinnedBuffer : AsyncIO.Buffer
    {
        private GCHandle m_handle;

        public PinnedBuffer(byte[] data) : base(data)
        {
            m_handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            BufferAddress = m_handle.AddrOfPinnedObject();
        }

        public IntPtr BufferAddress { get; private set; }

        public override void Dispose()
        {
            m_handle.Free();
        }
    }
}
