using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XNL.IOCP
{
  public class AsyncSocketStream : Stream
  {
    private AsyncSocket m_socket;

    internal AsyncSocketStream(AsyncSocket socket)
    {
      m_socket = socket;
    }

    ~AsyncSocketStream()
    {
      Dispose(false);
    }

    public override bool CanRead
    {
      get
      {
        return true;
      }
    }

    public override bool CanSeek
    {
      get
      {
        return false;
      }
    }

    public override bool CanWrite
    {
      get { return true; }
    }

    public override void Flush()
    {

    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {

    }   

    public override int Read(byte[] buffer, int offset, int count)
    {
      if (buffer == null)
        throw new ArgumentNullException("buffer");
      if (offset < 0 || offset > buffer.Length)
        throw new ArgumentOutOfRangeException("offset");
      if (count < 0 || count > buffer.Length - offset)
        throw new ArgumentOutOfRangeException("count");

      try
      {
        var receiveTask = m_socket.ReceiveAsync(buffer, offset, count, SocketFlags.None);
        receiveTask.Wait();
        return receiveTask.Result;
      }
      catch (AggregateException aggregateException)
      {
        Exception ex = aggregateException.Flatten().InnerException;

        if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
          throw;
        else
          throw new IOException("failure read from socket", ex);
      }
      catch (Exception ex)
      {
        if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
          throw;
        else
          throw new IOException("failure read from socket", ex);
      }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      if (buffer == null)
        throw new ArgumentNullException("buffer");
      if (offset < 0 || offset > buffer.Length)
        throw new ArgumentOutOfRangeException("offset");
      if (count < 0 || count > buffer.Length - offset)
        throw new ArgumentOutOfRangeException("count");

      cancellationToken.ThrowIfCancellationRequested();

      try
      {
        return await m_socket.ReceiveAsync(buffer, offset, offset, SocketFlags.None);
      }
      catch (AggregateException aggregateException)
      {
        Exception ex = aggregateException.Flatten().InnerException;

        if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
          throw;
        else
          throw new IOException("failure read from socket", ex);
      }
      catch (Exception ex)
      {
        if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
          throw;
        else
          throw new IOException("failure read from socket", ex);
      }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      if (buffer == null)
        throw new ArgumentNullException("buffer");
      if (offset < 0 || offset > buffer.Length)
        throw new ArgumentOutOfRangeException("offset");
      if (count < 0 || count > buffer.Length - offset)
        throw new ArgumentOutOfRangeException("count");

      try
      {
        var sendTask = m_socket.SendAsync(buffer, offset, count, SocketFlags.None);
        sendTask.Wait();
      }
      catch (AggregateException aggregateException)
      {
        Exception ex = aggregateException.Flatten().InnerException;

        if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
          throw;
        else
          throw new IOException("failure write to socket", ex);
      }
      catch (Exception ex)
      {
        if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
          throw;
        else
          throw new IOException("failure write from socket", ex);
      }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
      if (buffer == null)
        throw new ArgumentNullException("buffer");
      if (offset < 0 || offset > buffer.Length)
        throw new ArgumentOutOfRangeException("offset");
      if (count < 0 || count > buffer.Length - offset)
        throw new ArgumentOutOfRangeException("count");

      cancellationToken.ThrowIfCancellationRequested();

      try
      {
        await m_socket.SendAsync(buffer, offset, offset, SocketFlags.None);
      }
      catch (AggregateException aggregateException)
      {
        Exception ex = aggregateException.Flatten().InnerException;

        if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
          throw;
        else
          throw new IOException("failure write from socket", ex);
      }
      catch (Exception ex)
      {
        if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
          throw;
        else
          throw new IOException("failure write from socket", ex);
      }
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      return WriteAsync(buffer, offset, count).ToAsync(callback, state);
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
      Task task = (Task)asyncResult;

      if (task.IsFaulted)
      {
        throw task.Exception.InnerException;
      }      
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      return ReadAsync(buffer, offset, count).ToAsync(callback, state);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
      Task<int> task = (Task<int>)asyncResult;

      if (task.IsFaulted)
      {
        throw task.Exception.InnerException;
      }
      
      return task.Result;
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      m_socket = null;
    }

    public override long Length
    {
      get { throw new NotSupportedException(); }
    }

    public override long Position
    {
      get
      {
        throw new NotSupportedException();
      }
      set
      {
        throw new NotSupportedException();
      }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }
  }
}

