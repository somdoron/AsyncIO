using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace XNL.IOCP
{
  public class CompletionPort: IDisposable
  {
    private readonly IntPtr m_completionPortHandle;

    private readonly IntPtr InvalidCompletionPort = IntPtr.Zero;

    private readonly IntPtr InvalidCompletionPortMinusOne = new IntPtr(-1);
    
    private readonly UIntPtr NotifyPostCompletionKey= new UIntPtr(1);
    private int m_completionKeySequence = 2;

    private const int WaitTimeoutError = 258;
    private const int CompletionPortClosed = 735;

    private bool m_disposed = false;

    private static Func<Overlapped, IOCompletionCallback> s_getIOCompletionCallback;

    static CompletionPort()
    {
      s_getIOCompletionCallback = (Func<Overlapped, IOCompletionCallback>)Delegate.CreateDelegate(typeof(Func<Overlapped, IOCompletionCallback>), typeof(Overlapped).GetProperty("UserCallback",
                                                                                                   BindingFlags
                                                                                                     .NonPublic |
                                                                                                   BindingFlags.Instance)
                                                                                      .GetGetMethod(true));      
    }

    public CompletionPort(uint concurrentThreads = 1)
    {
      m_completionPortHandle =
        UnsafeMethods.CreateIoCompletionPort(UnsafeMethods.INVALID_HANDLE_VALUE, InvalidCompletionPort, UIntPtr.Zero, concurrentThreads);

      if (m_completionPortHandle == InvalidCompletionPort || m_completionPortHandle == InvalidCompletionPortMinusOne)
      {
        throw new Win32Exception();
      }
    }
    
    ~CompletionPort()
    {
      Dispose();
    }

    internal void AssociateFileHandle(IntPtr fileHandle)
    {
      int completionKey = Interlocked.Increment(ref m_completionKeySequence);

      IntPtr result = UnsafeMethods.CreateIoCompletionPort(fileHandle, m_completionPortHandle, new UIntPtr((uint)completionKey), 0);

      if (result == InvalidCompletionPort || result == InvalidCompletionPortMinusOne)
      {
        throw new Win32Exception();
      }
    }

    /// <summary>
    /// The method will process completion port packets until the timeout has reached, port has been notified or the completion port is closed 
    /// (in that case an exception will be thrown)    
    /// </summary>
    /// <param name="milliseconds"></param>   
    public bool Wait(int milliseconds)
    {
      uint numberOfBytes;
      UIntPtr completionKey;
      IntPtr overlapped;

      Stopwatch stopwatch = Stopwatch.StartNew();

      while (milliseconds== -1 || stopwatch.ElapsedMilliseconds < milliseconds)
      {
        int timeout = -1;

        if (milliseconds != -1)
        {
          timeout = milliseconds - (int)stopwatch.ElapsedMilliseconds;

          if (timeout < 0)
          {
            timeout = 0;
          }
        }

        bool result =
          UnsafeMethods.GetQueuedCompletionStatus(m_completionPortHandle, out numberOfBytes, out completionKey,
                                                  out overlapped, timeout);

        if (!result)
        {
          int error = Marshal.GetLastWin32Error();

          if (error == CompletionPortClosed)
          {
            throw new CompletionPortClosedException();
          }
          else if (error != WaitTimeoutError)
          {
            throw new Win32Exception(error);
          }
        }
        else
        {
          if (completionKey.Equals(NotifyPostCompletionKey))
          {
            return true;
          }
          else
          {
            InvokeCallback(overlapped);          
          }
        }
      }

      return false;
    }

    unsafe private void InvokeCallback(IntPtr nativeOverlappedAddress)
    {
      Overlapped overlapped = Overlapped.Unpack((NativeOverlapped*)nativeOverlappedAddress);

      NativeOverlapped* pNativeOverlapped = ((NativeOverlapped*)nativeOverlappedAddress);

      NativeOverlapped nativeOverlapped = *pNativeOverlapped;

      s_getIOCompletionCallback(overlapped)((uint)nativeOverlapped.InternalLow.ToInt32(),
        (uint)nativeOverlapped.InternalHigh.ToInt32(), pNativeOverlapped);
    }    

    public void NotifyOnce()
    {
      UnsafeMethods.PostQueuedCompletionStatus(m_completionPortHandle, 0, NotifyPostCompletionKey, IntPtr.Zero);
    }

    public void Dispose()
    {
      if (!m_disposed)
      {
        m_disposed = true;

        UnsafeMethods.CloseHandle(m_completionPortHandle);        

        GC.SuppressFinalize(this);
      }
    }
  }
}
