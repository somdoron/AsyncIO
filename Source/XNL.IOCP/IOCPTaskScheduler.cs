using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XNL.IOCP
{
  /// <summary>
  /// IOCP Task Scheduler
  /// </summary>
  public class IOCPTaskScheduler : TaskScheduler, IDisposable
  {
    private readonly CompletionPort m_completionPort;

    private readonly bool m_ownCompletionPort;
    private bool m_disposed = false;
    private readonly int m_numAvailableThreads;

    private readonly ConcurrentQueue<Task> m_queue = new ConcurrentQueue<Task>();

    readonly ThreadLocal<bool> m_isSchedulerThread = new ThreadLocal<bool>(() => false);

    readonly CancellationTokenSource m_cancellationTokenSource = new CancellationTokenSource();

    readonly List<Task> m_workerTasks = new List<Task>();
    private IOCPSynchronizationContext m_synchronizationContext;

    private IOCPTaskScheduler(int numAvailableThreads)
    {
      m_numAvailableThreads = numAvailableThreads;
    }

    /// <summary>
    /// Create new IOCP TaskScheduler, the completion port will be created and own by the Task Scheduler.
    /// </summary>
    /// <param name="numAvailableThreads">Number of threads that will be created by the task scheduler</param>
    /// <param name="maxConcurrencyLevel">The maximum number of threads that the operation system can allow to curcurrently process I/O completion packets.</param>    
    public IOCPTaskScheduler(int numAvailableThreads, int maxConcurrencyLevel)
      : this(numAvailableThreads)
    {
      m_completionPort = new CompletionPort((uint)maxConcurrencyLevel);
      m_ownCompletionPort = true;

      Start();
    }

    /// <summary>
    /// Create new IOCP Task Scheduler, the completion port is provided. The user is responsible to dispose the completion port at the end of the use.
    /// </summary>
    /// <param name="completionPort">Completion port the Task Scheduler will use as the blocking method</param>
    /// <param name="numAvailableThreads">Number of threads that will be created by the task scheduler</param>
    public IOCPTaskScheduler(CompletionPort completionPort, int numAvailableThreads)
      : this(numAvailableThreads)
    {
      m_completionPort = completionPort;
      m_ownCompletionPort = false;

      Start();
    }

    ~IOCPTaskScheduler()
    {
      Dispose();
    }

    internal CompletionPort CompletionPort
    {
      get { return m_completionPort; }
    }

    private void Start()
    {
      m_synchronizationContext = new IOCPSynchronizationContext(this);

      for (int i = 0; i < m_numAvailableThreads; i++)
      {
        Task task = new Task(Worker, TaskCreationOptions.LongRunning);
        task.Start(TaskScheduler.Default);

        m_workerTasks.Add(task);
      }
    }

    private void Worker()
    {      
      SynchronizationContext.SetSynchronizationContext(m_synchronizationContext);

      m_isSchedulerThread.Value = true;      

      var cancellationToken = m_cancellationTokenSource.Token;

      while (!cancellationToken.IsCancellationRequested)
      {
        try
        {
          var isNotifed = m_completionPort.Wait(-1);

          if (isNotifed)
          {
            Task task;

            if (m_queue.TryDequeue(out task))
            {
              TryExecuteTask(task);
            }
          }
        }
        catch (CompletionPortClosedException)
        {
          break;
        }
      }

      m_isSchedulerThread.Value = false;
    }

    protected override void QueueTask(Task task)
    {
      if (!TryExecuteTaskInline(task, false))
      {
        m_queue.Enqueue(task);

        m_completionPort.NotifyOnce();
      }
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
      return m_queue.ToArray();
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
      return m_isSchedulerThread.Value && TryExecuteTask(task);
    }

    /// <summary>
    /// Close all the threads and close the completion port if owned by Task Scheduler
    /// </summary>
    public void Dispose()
    {
      if (!m_disposed)
      {
        m_disposed = false;
        m_cancellationTokenSource.Cancel();

        for (int i = 0; i < m_numAvailableThreads; i++)
        {
          m_completionPort.NotifyOnce();
        }

        Task.WaitAll(m_workerTasks.ToArray());

        if (m_ownCompletionPort)
        {
          m_completionPort.Dispose();
        }

        GC.SuppressFinalize(this);
      }
    }
  }
}
