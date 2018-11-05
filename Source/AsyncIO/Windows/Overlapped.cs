using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AsyncIO.Windows
{
    class Overlapped : IDisposable
    {        
        private static readonly int Size = IntPtr.Size * 4 + sizeof(int) * 2;
        private static readonly int BytesTransferredOffset = IntPtr.Size;
        private static readonly int OffsetOffset = IntPtr.Size * 2;
        private static readonly int EventOffset = IntPtr.Size * 2 + sizeof(int) * 2;

        private static readonly int ManagedOverlappedOffset = IntPtr.Size * 3 + sizeof(int) * 2;                

        private IntPtr m_address;
        private GCHandle m_handle;
        private PinnedBuffer m_pinnedBuffer;

        public Overlapped(Windows.Socket asyncSocket)
        {
            Disposed = false;
            InProgress = false;
            AsyncSocket = asyncSocket;
            m_address = Marshal.AllocHGlobal(Size);
            Marshal.WriteIntPtr(m_address, IntPtr.Zero);
            Marshal.WriteIntPtr(m_address,BytesTransferredOffset, IntPtr.Zero);
            Marshal.WriteInt64(m_address, OffsetOffset, 0);
            Marshal.WriteIntPtr(m_address, EventOffset, IntPtr.Zero);

            m_handle = GCHandle.Alloc(this, GCHandleType.Normal);

            Marshal.WriteIntPtr(m_address, ManagedOverlappedOffset, GCHandle.ToIntPtr(m_handle));            
        }

        public void Dispose()
        {
            if (!InProgress)
            {
                Free();
            }

            Disposed = true;            
        }

        private void Free()
        {
            Marshal.FreeHGlobal(m_address);

            if (m_handle.IsAllocated)
            {
                m_handle.Free();
            }

            if (m_pinnedBuffer != null)
            {
                m_pinnedBuffer.Dispose();
                m_pinnedBuffer = null;
            }
        }

        public IntPtr Address
        {
            get { return m_address; }
        }

        public long BufferAddress
        {
            get { return m_pinnedBuffer != null ? m_pinnedBuffer.Address : 0; }
        }

        public OperationType OperationType { get; private set; }

        public Windows.Socket AsyncSocket { get; private set; }

        public bool Success { get; private set; }

        public bool InProgress { get; private set; }

        public bool Disposed { get; private set; }

        public object State { get; set; }

        public void StartOperation(OperationType operationType)
        {
            StartOperation(operationType, null);

        }
        public void StartOperation(OperationType operationType, byte[] buffer)
        {
            InProgress = true;
            Success = false;
            OperationType = operationType;

            if (buffer != null)
            {
                if (m_pinnedBuffer == null)
                {
                    m_pinnedBuffer = new PinnedBuffer(buffer);
                }
                else if (m_pinnedBuffer.Buffer != buffer)
                {
                    m_pinnedBuffer.Switch(buffer);
                }
            }
        }

        public static Overlapped CompleteOperation(IntPtr overlappedAddress)
        {
            IntPtr managedOverlapped = Marshal.ReadIntPtr(overlappedAddress, ManagedOverlappedOffset);

            GCHandle handle = GCHandle.FromIntPtr(managedOverlapped);

            Overlapped overlapped = (Overlapped) handle.Target;

            overlapped.InProgress = false;

            if (overlapped.Disposed)
            {
                overlapped.Free();
                overlapped.Success = false;
            }
            else
            {
                overlapped.Success = Marshal.ReadIntPtr(overlapped.m_address).Equals(IntPtr.Zero);
            }

            return overlapped;          
        }        
    }
}