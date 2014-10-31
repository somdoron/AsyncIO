using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AsyncIO.Windows
{
    class Overlapped : IDisposable
    {
        private static readonly int Size = IntPtr.Size * 4 + sizeof(int) * 3;

        private static readonly int BytesTransferredOffset = IntPtr.Size;
        private static readonly int OffsetOffset = IntPtr.Size * 2;
        private static readonly int EventOffset = IntPtr.Size * 2 + sizeof(int) * 2;

        private static readonly int OperationTypeOffset = IntPtr.Size * 3 + sizeof(int) * 2;        
        private static readonly int StateOffset = IntPtr.Size * 3 + sizeof(int) * 3;

        private IntPtr m_address;

        private GCHandle m_stateHandle;
        private GCHandle m_socketHandle;

        public Overlapped()
        {
            m_address = Marshal.AllocHGlobal(Size);
            Marshal.WriteIntPtr(m_address, IntPtr.Zero);
            Marshal.WriteIntPtr(m_address, OperationTypeOffset, IntPtr.Zero);
            Marshal.WriteInt64(m_address, OffsetOffset, 0);
            Marshal.WriteIntPtr(m_address, EventOffset, IntPtr.Zero);
            Marshal.WriteIntPtr(m_address, StateOffset, IntPtr.Zero);            
        }

        public void Dispose()
        {         
            Marshal.WriteIntPtr(m_address, StateOffset, IntPtr.Zero);

            Marshal.FreeHGlobal(m_address);

            if (m_stateHandle.IsAllocated)
            {
                m_stateHandle.Free();
            }

            if (m_socketHandle.IsAllocated)
            {
                m_socketHandle.Free();
            }
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

        public SocketError SocketError
        {
            set
            {
                Marshal.WriteIntPtr(m_address, new IntPtr((int)value));
            }
        }

        public object State
        {
            set
            {
                if (m_stateHandle.IsAllocated)
                {
                    m_stateHandle.Free();
                }

                if (value != null)
                {
                    m_stateHandle = GCHandle.Alloc(value);
                    Marshal.WriteIntPtr(m_address, StateOffset, GCHandle.ToIntPtr(m_stateHandle));
                }
                else
                {
                    m_stateHandle = new GCHandle();
                    Marshal.WriteIntPtr(m_address, StateOffset, IntPtr.Zero);
                }
            }
        }

        public static void Read(IntPtr overlapped, out OperationType operationType, out int bytesTransferred, out object state)
        {
            bytesTransferred = (int)Marshal.ReadIntPtr(overlapped, BytesTransferredOffset);
            operationType = (OperationType)Marshal.ReadInt32(overlapped, OperationTypeOffset);

            state = GetObjectFromAddress<object>(overlapped, StateOffset);                    

            //if (IntPtr.Size == 4)
            //{
            //    socketError = (System.Net.Sockets.SocketError)Marshal.ReadInt32(overlapped);
            //}
            //else
            //{
            //    socketError = (System.Net.Sockets.SocketError)(Marshal.ReadInt64(overlapped) & 0x7FFFFFFF);
            //}
        }

        private static T GetObjectFromAddress<T>(IntPtr address, int offset)
        {
            IntPtr objectAddress = Marshal.ReadIntPtr(address, offset);

            if (objectAddress != IntPtr.Zero)
            {
                GCHandle handle = GCHandle.FromIntPtr(objectAddress);
                return (T)handle.Target;
            }

            return default(T);
        }


    }
}