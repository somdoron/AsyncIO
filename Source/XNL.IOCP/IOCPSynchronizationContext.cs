using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XNL.IOCP
{
  class IOCPSynchronizationContext : SynchronizationContext
  {
    private readonly IOCPTaskScheduler m_iocpTaskScheduler;

    public IOCPSynchronizationContext(IOCPTaskScheduler iocpTaskScheduler)
    {
      m_iocpTaskScheduler = iocpTaskScheduler;     
    }

    public override void Post(SendOrPostCallback d, object state)
    {
      if (object.ReferenceEquals(Current, this))
      {
        d(state);
      }
      else
      {
        Task task = new Task(() => d(state));
        task.Start(m_iocpTaskScheduler);  
      }      
    }
  }
}
