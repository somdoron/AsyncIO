using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AsyncIO.Windows
{
    class Overlapped : IDisposable
    {
        private static readonly int Size = IntPtr.Size * 3 + sizeof(int) * 3;
        private static readonly int OperationTypeOffset = IntPtr.Size * 3 + sizeof(int) * 2;
        private static readonly int BytesTransferredOffset = IntPtr.Size;
        private static readonly int OffsetOffset = IntPtr.Size*2;
        private static readonly int EventOffset = IntPtr.Size * 2 + sizeof(int) * 2;

        private IntPtr m_address;

        public Overlapped()
        {
            m_address = Marshal.AllocHGlobal(Size);
            Marshal.WriteIntPtr(m_address, IntPtr.Zero);
            Marshal.WriteIntPtr(m_address, OperationTypeOffset, IntPtr.Zero);
            Marshal.WriteInt64(m_address, OffsetOffset, 0);
            Marshal.WriteIntPtr(m_address, EventOffset, IntPtr.Zero);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(m_address);
        }

        public IntPtr Address
        {
            get { return m_address; }
        }

        public OperationType OperationType
        {
            set
            {
                Marshal.WriteInt32(m_address, OperationTypeOffset, (int)value);
            }
        }



        public static void Read(IntPtr overlapped, out OperationType operationType, out SocketError socketError, out int bytesTransferred)
        {
            bytesTransferred = (int)Marshal.ReadIntPtr(overlapped, BytesTransferredOffset);
            socketError = (System.Net.Sockets.SocketError)Marshal.ReadIntPtr(overlapped);
            operationType = (OperationType)Marshal.ReadInt32(overlapped, OperationTypeOffset);            
        }

    }
}