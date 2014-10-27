using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XNL.IOCP
{
  static class TaskExtensions
  {
    public static void SetFromTask<TResult>(this TaskCompletionSource<TResult> resultSetter, Task task)
    {
      switch (task.Status)
      {
        case TaskStatus.RanToCompletion: resultSetter.SetResult(task is Task<TResult> ? ((Task<TResult>)task).Result : default(TResult)); break;
        case TaskStatus.Faulted: resultSetter.SetException(task.Exception.InnerExceptions); break;
        case TaskStatus.Canceled: resultSetter.SetCanceled(); break;
        default: throw new InvalidOperationException("The task was not completed.");
      }
    }

    public static void SetFromTask<TResult>(this TaskCompletionSource<TResult> resultSetter, Task<TResult> task)
    {
      SetFromTask(resultSetter, (Task)task);
    }


    public static Task ToAsync(this Task task, AsyncCallback callback, object state)
    {
      if (task == null) throw new ArgumentNullException("task");

      var tcs = new TaskCompletionSource<object>(state);
      task.ContinueWith(_ =>
      {
        tcs.SetFromTask(task);
        if (callback != null) callback(tcs.Task);
      });
      return tcs.Task;
    }

    public static Task<TResult> ToAsync<TResult>(this Task<TResult> task, AsyncCallback callback, object state)
    {
      if (task == null) throw new ArgumentNullException("task");

      var tcs = new TaskCompletionSource<TResult>(state);
      task.ContinueWith(_ =>
      {
        tcs.SetFromTask(task);
        if (callback != null) callback(tcs.Task);
      });
      return tcs.Task;
    }
  }
}
