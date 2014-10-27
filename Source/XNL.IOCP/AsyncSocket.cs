using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace XNL.IOCP
{
  public class AsyncSocket : IDisposable
  {
    private readonly AddressFamily m_addressFamily;
    private readonly SocketType m_socketType;
    private readonly ProtocolType m_protocolType;

    private ConnectExDelegate m_connectEx;
    private AcceptExDelegate m_acceptEx;

    private TaskCompletionSource<int> m_sendAsyncTaskCompletionSource;
    private AsyncOperationState m_sendAsyncOperationState;

    private TaskCompletionSource<int> m_receiveAsyncTaskCompletionSource;
    private AsyncOperationState m_receiveAsyncOperationState;

    private TaskCompletionSource<bool> m_connectAsyncTaskCompletionSource;
    private AsyncOperationState m_connectAsyncOperationState;

    private TaskCompletionSource<AsyncSocket> m_acceptAsyncTaskCompletionSource;
    private AsyncOperationState m_acceptAsyncOperationState;

    private SocketAddress m_remoteAddress;
    private SocketAddress m_boundAddress;

    private bool m_disposed = false;
    private AsyncSocket m_acceptedSocket;
    private int m_acceptSocketBufferSize;
    private byte[] m_acceptSocketBuffer;
    private IntPtr m_acceptSocketBufferAddress;

    private AsyncSocketStream m_asyncSocketStream;

    public AsyncSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
    {
      m_addressFamily = addressFamily;
      m_socketType = socketType;
      m_protocolType = protocolType;

      InitSocket();
      InitDynamicMethods();
    }

    ~AsyncSocket()
    {
      Dispose(false);
    }

    public IntPtr Handle { get; private set; }

    private void InitSocket()
    {
      Handle = UnsafeMethods.WSASocket(m_addressFamily, m_socketType, m_protocolType, IntPtr.Zero, 0,
                                       SocketConstructorFlags.WSA_FLAG_OVERLAPPED);

      if (Handle == UnsafeMethods.INVALID_HANDLE_VALUE)
      {
        throw new SocketException();
      }
    }

    private void InitDynamicMethods()
    {
      m_connectEx =
        (ConnectExDelegate)LoadDynamicMethod(UnsafeMethods.WSAID_CONNECTEX, typeof(ConnectExDelegate));

      m_acceptEx =
        (AcceptExDelegate)LoadDynamicMethod(UnsafeMethods.WSAID_ACCEPT_EX, typeof(AcceptExDelegate));
    }

    private Delegate LoadDynamicMethod(Guid guid, Type type)
    {
      IntPtr connectExAddress = IntPtr.Zero;
      int byteTransfered = 0;

      SocketError socketError = (SocketError)UnsafeMethods.WSAIoctl(Handle, UnsafeMethods.GetExtensionFunctionPointer, ref guid, Marshal.SizeOf(guid),
                             ref connectExAddress, IntPtr.Size, ref byteTransfered, IntPtr.Zero, IntPtr.Zero);

      if (socketError != SocketError.Success)
      {
        throw new SocketException();
      }

      return Marshal.GetDelegateForFunctionPointer(connectExAddress, type);
    }

    public AsyncSocketStream GetStream()
    {
      if (m_asyncSocketStream == null)
      {
        m_asyncSocketStream = new AsyncSocketStream(this);
      }

      return m_asyncSocketStream;
    }

    public void BindToThreadPool()
    {
      ThreadPool.BindHandle(Handle);
    }

    public void BindToCompletionPort(CompletionPort completionPort)
    {
      completionPort.AssociateFileHandle(this.Handle);
    }

    public void Bind()
    {
      IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

      if (m_addressFamily == AddressFamily.InterNetworkV6)
      {
        endPoint = new IPEndPoint(IPAddress.IPv6Any, 0);
      }

      Bind(endPoint);
    }

    public void Bind(IPEndPoint endPoint)
    {
      if (m_boundAddress != null)
      {
        m_boundAddress.Dispose();
        m_boundAddress = null;
      }

      m_boundAddress = new SocketAddress(endPoint.Address, endPoint.Port);

      SocketError bindResult = (SocketError)UnsafeMethods.bind(Handle, m_boundAddress.Buffer, m_boundAddress.Size);

      if (bindResult != SocketError.Success)
      {
        throw new SocketException((int)bindResult);
      }
    }

    public void Listen(int backlog)
    {
      SocketError bindResult = (SocketError)UnsafeMethods.listen(Handle,
        backlog);

      if (bindResult != SocketError.Success)
      {
        throw new SocketException();
      }
    }

    public Task<AsyncSocket> AcceptAsync()
    {
      if (m_acceptAsyncOperationState == null)
      {
        m_acceptAsyncOperationState = new AsyncOperationState(this, CompleteAcceptAsync);

        m_acceptSocketBufferSize = (m_boundAddress.Size + 16) * 2;

        m_acceptSocketBuffer = new byte[m_acceptSocketBufferSize];

        GCHandle.Alloc(m_acceptSocketBuffer, GCHandleType.Pinned);

        m_acceptSocketBufferAddress = Marshal.UnsafeAddrOfPinnedArrayElement(
          (Array)m_acceptSocketBuffer, 0);
      }

      SocketError socketError = SocketError.Success;

      m_acceptAsyncTaskCompletionSource = new TaskCompletionSource<AsyncSocket>();
      Task<AsyncSocket> task = m_acceptAsyncTaskCompletionSource.Task;

      m_acceptAsyncOperationState.PrepareForCall();

      m_acceptedSocket = new AsyncSocket(m_addressFamily, m_socketType, m_protocolType);

      int bytesReceived;

      try
      {
        if (!
          m_acceptEx(Handle, m_acceptedSocket.Handle, m_acceptSocketBufferAddress, 0,
          m_acceptSocketBufferSize / 2,
          m_acceptSocketBufferSize / 2, out bytesReceived,
                        m_acceptAsyncOperationState.OverlappdAddress))
        {
          socketError = (SocketError)Marshal.GetLastWin32Error();
        }
      }
      catch (Exception ex)
      {
        m_acceptAsyncTaskCompletionSource = null;
        m_acceptedSocket.Dispose();
        m_acceptedSocket = null;
        throw;
      }

      if (socketError != SocketError.IOPending && socketError != SocketError.Success)
      {
        m_acceptAsyncTaskCompletionSource.SetException(new SocketException((int)socketError));
        m_acceptAsyncTaskCompletionSource = null;
        m_acceptedSocket.Dispose();
        m_acceptedSocket = null;
      }

      return task;
    }

    private void CompleteAcceptAsync(uint errorCode, uint bytesTransfered)
    {
      var taskCompletionSource = m_acceptAsyncTaskCompletionSource;
      var acceptedSocket = m_acceptedSocket;

      m_acceptedSocket = null;
      m_acceptAsyncTaskCompletionSource = null;

      SocketError error = (SocketError)errorCode;

      if (error != SocketError.Success)
      {
        taskCompletionSource.SetException(new SocketException((int)error));
        acceptedSocket.Dispose();
      }
      else
      {
        taskCompletionSource.SetResult(acceptedSocket);
      }
    }

    public Task ConnectAsync(IPEndPoint endPoint)
    {
      if (m_connectAsyncOperationState == null)
      {
        m_connectAsyncOperationState = new AsyncOperationState(this, CompleteConnectAsync);
      }

      if (m_remoteAddress != null)
      {
        m_remoteAddress.Dispose();
        m_remoteAddress = null;
      }

      m_remoteAddress = new SocketAddress(endPoint.Address, endPoint.Port);

      int bytesSend;

      SocketError socketError = SocketError.Success;

      m_connectAsyncTaskCompletionSource = new TaskCompletionSource<bool>();
      Task task = m_connectAsyncTaskCompletionSource.Task;

      m_connectAsyncOperationState.PrepareForCall();

      try
      {
        if (!m_connectEx(Handle, m_remoteAddress.Buffer, m_remoteAddress.Size, IntPtr.Zero, 0,
                        out bytesSend,
                        m_connectAsyncOperationState.OverlappdAddress))
        {
          socketError = (SocketError)Marshal.GetLastWin32Error();
        }
      }
      catch (Exception ex)
      {
        m_connectAsyncTaskCompletionSource = null;
        throw;
      }

      if (socketError != SocketError.IOPending && socketError != SocketError.Success)
      {
        m_connectAsyncTaskCompletionSource.SetException(new SocketException((int)socketError));
        m_connectAsyncTaskCompletionSource = null;
      }

      return task;
    }

    private void CompleteConnectAsync(uint errorCode, uint bytesTransfered)
    {
      var taskCompletionSource = m_connectAsyncTaskCompletionSource;
      m_connectAsyncTaskCompletionSource = null;

      SocketError error = (SocketError)errorCode;

      if (error != SocketError.Success)
      {
        taskCompletionSource.SetException(new SocketException((int)error));
      }
      else
      {
        taskCompletionSource.SetResult(true);
      }
    }

    public Task<int> ReceiveAsync(byte[] buffer, int offset, int size, SocketFlags flags)
    {
      if (m_receiveAsyncOperationState == null)
      {
        m_receiveAsyncOperationState = new AsyncOperationState(this, CompleteReceiveAsync);
      }

      if (buffer == null)
        throw new ArgumentNullException("buffer");

      m_receiveAsyncOperationState.SetBuffer(buffer, offset, size);

      int bytesTransferred;

      SocketError sendResult;

      m_receiveAsyncOperationState.PrepareForCall();

      m_receiveAsyncTaskCompletionSource = new TaskCompletionSource<int>();
      Task<int> task = m_receiveAsyncTaskCompletionSource.Task;

      try
      {
        sendResult = UnsafeMethods.WSARecv(Handle, ref m_receiveAsyncOperationState.WSABuffer, 1,
          out bytesTransferred, ref flags, m_receiveAsyncOperationState.OverlappdAddress, IntPtr.Zero);
      }
      catch (Exception ex)
      {
        m_receiveAsyncTaskCompletionSource = null;
        throw;
      }

      if (sendResult != SocketError.Success)
      {
        sendResult = (SocketError)Marshal.GetLastWin32Error();
      }

      if (sendResult != SocketError.Success && sendResult != SocketError.IOPending)
      {
        m_receiveAsyncTaskCompletionSource.SetException(new SocketException((int)sendResult));
        m_receiveAsyncTaskCompletionSource = null;
      }

      return task;
    }

    private void CompleteReceiveAsync(uint errorCode, uint bytesTransfered)
    {
      var taskCompletionSource = m_receiveAsyncTaskCompletionSource;
      m_sendAsyncTaskCompletionSource = null;

      SocketError error = (SocketError)errorCode;

      if (error != SocketError.Success)
      {
        taskCompletionSource.SetException(new SocketException((int)error));
      }
      else
      {
        taskCompletionSource.SetResult((int)bytesTransfered);
      }
    }

    public Task<int> SendAsync(byte[] buffer, int offset, int size, SocketFlags flags)
    {
      if (m_sendAsyncOperationState == null)
      {
        m_sendAsyncOperationState = new AsyncOperationState(this, CompleteSyndAsync);
      }

      if (buffer == null)
        throw new ArgumentNullException("buffer");

      m_sendAsyncOperationState.SetBuffer(buffer, offset, size);

      int bytesTransferred;

      SocketError sendResult;

      m_sendAsyncOperationState.PrepareForCall();

      m_sendAsyncTaskCompletionSource = new TaskCompletionSource<int>();
      Task<int> task = m_sendAsyncTaskCompletionSource.Task;

      try
      {
        sendResult = UnsafeMethods.WSASend(Handle, ref m_sendAsyncOperationState.WSABuffer, 1,
          out bytesTransferred, flags, m_sendAsyncOperationState.OverlappdAddress, IntPtr.Zero);
      }
      catch (Exception ex)
      {
        m_sendAsyncTaskCompletionSource = null;
        throw;
      }

      if (sendResult != SocketError.Success)
      {
        sendResult = (SocketError)Marshal.GetLastWin32Error();
      }

      if (sendResult != SocketError.Success && sendResult != SocketError.IOPending)
      {
        m_sendAsyncTaskCompletionSource.SetException(new SocketException((int)sendResult));
        m_sendAsyncTaskCompletionSource = null;
      }

      return task;
    }

    private void CompleteSyndAsync(uint errorCode, uint bytesTransfered)
    {
      var taskCompletionSource = m_sendAsyncTaskCompletionSource;
      m_sendAsyncTaskCompletionSource = null;

      SocketError error = (SocketError)errorCode;

      if (error != SocketError.Success)
      {
        taskCompletionSource.SetException(new SocketException((int)error));
      }
      else
      {
        taskCompletionSource.SetResult((int)bytesTransfered);
      }
    }

    private void Dispose(bool disposing)
    {
      if (!m_disposed)
      {
        m_disposed = true;

        if (m_connectAsyncOperationState != null)
        {
          m_connectAsyncOperationState.Dispose();
          m_connectAsyncOperationState = null;
        }

        if (m_sendAsyncOperationState != null)
        {
          m_sendAsyncOperationState.Dispose();
          m_sendAsyncOperationState = null;
        }

        if (m_receiveAsyncOperationState != null)
        {
          m_receiveAsyncOperationState.Dispose();
          m_receiveAsyncOperationState = null;
        }

        if (m_acceptAsyncOperationState != null)
        {
          m_acceptAsyncOperationState.Dispose();
          m_acceptAsyncOperationState = null;
        }

        if (m_remoteAddress != null)
        {
          m_remoteAddress.Dispose();
          m_remoteAddress = null;
        }

        if (m_boundAddress != null)
        {
          m_boundAddress.Dispose();
          m_boundAddress = null;
        }

        UnsafeMethods.closesocket(Handle);
      }
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }
  }
}
