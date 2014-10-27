using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace XNL.IOCP
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

  internal static class UnsafeMethods
  {
    public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    public static readonly Guid WSAID_CONNECTEX = new Guid("{0x25a207b9,0x0ddf3,0x4660,{0x8e,0xe9,0x76,0xe5,0x8c,0x74,0x06,0x3e}}");

    public static readonly Guid WSAID_ACCEPT_EX =
      new Guid("{0xb5367df1,0xcbac,0x11cf,{0x95, 0xca, 0x00, 0x80, 0x5f, 0x48, 0xa1, 0x92}}");

    public const int GetExtensionFunctionPointer = -939524090;


    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateIoCompletionPort(IntPtr fileHandle, IntPtr existingCompletionPort,
                                                       UIntPtr completionKey, uint numberOfConcurrentThreads);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetQueuedCompletionStatus(IntPtr completionPort, out uint numberOfBytes,
                                                        out UIntPtr completionKey, out IntPtr overlapped,
                                                        int milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool PostQueuedCompletionStatus(IntPtr completionPort,
                                                         uint numberOfBytesTransferred, UIntPtr completionKey, IntPtr overlapped);

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

    [DllImport("Ws2_32.dll")]
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

    [DllImport("Ws2_32.dll")]
    public static extern int bind(IntPtr s, byte[] socketAddress, int addrsize);

    [DllImport("Ws2_32.dll")]
    public static extern int listen(IntPtr s, int backlog);

    [DllImport("ws2_32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public extern static IntPtr WSASocket(
      [In] AddressFamily addressFamily, [In] SocketType socketType, [In] ProtocolType protocolType,
      [In] IntPtr pinnedBuffer,
      [In] uint group, [In] SocketConstructorFlags flags);

    [DllImport("ws2_32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int closesocket(IntPtr s);
  }
}