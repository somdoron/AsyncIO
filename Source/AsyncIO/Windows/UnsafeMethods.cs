using System;
using System.Net.Sockets;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace AsyncIO.Windows
{
    internal delegate bool ConnectExDelegate(IntPtr socketHandle,
      byte[] socketAddress, int socketAddressSize,
      IntPtr buffer, int dataLength, out int bytesSent, IntPtr overlapped);

    internal delegate bool AcceptExDelegate(IntPtr listenSocketHandle,
      IntPtr acceptSocketHandle,
      IntPtr buffer, int len,
      int localAddressLength,
      int remoteAddressLength,
      out int bytesReceived, IntPtr overlapped);

    [System.Flags]
    internal enum SocketConstructorFlags
    {
        WSA_FLAG_OVERLAPPED = 1,
        WSA_FLAG_MULTIPOINT_C_ROOT = 2,
        WSA_FLAG_MULTIPOINT_C_LEAF = 4,
        WSA_FLAG_MULTIPOINT_D_ROOT = 8,
        WSA_FLAG_MULTIPOINT_D_LEAF = 16,
    }

    internal struct WSABuffer
    {
        internal int Length;
        internal IntPtr Pointer;
    }

    internal struct Linger
    {
        internal ushort OnOff;
        internal ushort Time;
    }

    internal struct IPv6MulticastRequest
    {
        internal static readonly int Size = Marshal.SizeOf(typeof(IPv6MulticastRequest));
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal byte[] MulticastAddress;
        internal int InterfaceIndex;

        static IPv6MulticastRequest()
        {
        }
    }

    internal struct IPMulticastRequest
    {
        internal static readonly int Size = Marshal.SizeOf(typeof(IPMulticastRequest));
        internal int MulticastAddress;
        internal int InterfaceAddress;

        static IPMulticastRequest()
        {
        }
    }

    struct OverlappedEntry
    {
        public IntPtr CompletionKey;
        public IntPtr Overlapped;
        public IntPtr Internal;
        public int BytesTransferred;
    }

    internal static class UnsafeMethods
    {
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public static readonly Guid WSAID_CONNECTEX = new Guid("25a207b9-ddf3-4660-8ee9-76e58c74063e");
        public static readonly Guid WSAID_ACCEPT_EX = new Guid("b5367df1-cbac-11cf-95ca-00805f48a192");

        public const int GetExtensionFunctionPointer = -939524090;


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateIoCompletionPort(IntPtr fileHandle, IntPtr existingCompletionPort,
                                                           IntPtr completionKey, uint numberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetQueuedCompletionStatus(IntPtr completionPort, out int numberOfBytes,
                                                            out IntPtr completionKey, out IntPtr overlapped,
                                                            int milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetQueuedCompletionStatusEx(IntPtr completionPort, IntPtr completionPortEntries,
                                                            int count, out int removoed, int milliseconds, bool alertable);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool PostQueuedCompletionStatus(IntPtr completionPort, int numberOfBytesTransferred,
            IntPtr completionKey, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError WSASend(
          [In] IntPtr socketHandle,
          [In] ref WSABuffer buffer, [In] int bufferCount,
          out int bytesTransferred,
          [In] SocketFlags socketFlags,
          [In] IntPtr overlapped,
          [In] IntPtr completionRoutine);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError WSARecv(
          [In] IntPtr socketHandle,
          [In] ref WSABuffer buffer, [In] int bufferCount,
          out int bytesTransferred,
          [In, Out] ref SocketFlags socketFlags,
          [In] IntPtr overlapped,
          [In] IntPtr completionRoutine);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        public static extern int WSAIoctl(
            /* Socket, Mode */
            IntPtr s, int dwIoControlCode,
            /* Optional Or IntPtr.Zero, 0 */
            ref Guid lpvInBuffer, int cbInBuffer,
            /* Optional Or IntPtr.Zero, 0 */
            ref IntPtr lpvOutBuffer, int cbOutBuffer,
            /* reference to receive Size */
            ref int lpcbBytesReturned,
            /* IntPtr.Zero, IntPtr.Zero */
            IntPtr lpOverlapped, IntPtr lpCompletionRoutine);

        [DllImport("ws2_32.dll", EntryPoint = "WSAIoctl", SetLastError = true)]
        public static extern SocketError WSAIoctl_Blocking([In] IntPtr socketHandle, [In] int ioControlCode, [In] byte[] inBuffer, [In] int inBufferSize, [Out] byte[] outBuffer, [In] int outBufferSize, out int bytesTransferred, [In] IntPtr overlapped, [In] IntPtr completionRoutine);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        public static extern int bind(IntPtr s, byte[] socketAddress, int addrsize);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        public static extern int listen(IntPtr s, int backlog);

        [DllImport("ws2_32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public extern static IntPtr WSASocket(
          [In] AddressFamily addressFamily, [In] SocketType socketType, [In] ProtocolType protocolType,
          [In] IntPtr pinnedBuffer,
          [In] uint group, [In] SocketConstructorFlags flags);

        [DllImport("ws2_32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int closesocket(IntPtr s);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError setsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName, [In] byte[] optionValue, [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError setsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel,
              [In] SocketOptionName optionName, [In] ref int optionValue, [In] int optionLength);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError setsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] ref Linger linger, [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError setsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] ref IPv6MulticastRequest mreq, [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError setsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] ref IPMulticastRequest mreq, [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError getsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, out int optionValue, [In, Out] ref int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError getsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [Out] byte[] optionValue, [In, Out] ref int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError getsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, out Linger optionValue, [In, Out] ref int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError getsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, out IPMulticastRequest optionValue, [In, Out] ref int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError getsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, out IPv6MulticastRequest optionValue, [In, Out] ref int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError getsockname([In] IntPtr socketHandle, [Out] byte[] socketAddress, [In, Out] ref int socketAddressSize);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern SocketError getpeername([In] IntPtr socketHandle, [Out] byte[] socketAddress, [In, Out] ref int socketAddressSize);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern bool WSAGetOverlappedResult(
                                                 [In] IntPtr socketHandle,
                                                 [In] IntPtr overlapped,
                                                 [Out] out int bytesTransferred,
                                                 [In] bool wait,
                                                 [Out] out SocketFlags socketFlags
                                                 );

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(IntPtr hFile, IntPtr overlapped);

        [DllImport("ws2_32.dll", SetLastError = false)]
        internal static extern int WSAGetLastError();

        [DllImport("kernel32.dll")]
        public static extern bool CancelIo(IntPtr hFile);
    }
}