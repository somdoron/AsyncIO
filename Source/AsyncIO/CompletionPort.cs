using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;


namespace AsyncIO
{
    public abstract class CompletionPort : IDisposable
    {
        public static CompletionPort Create()
        {
#if NETSTANDARD1_3
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#else
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || ForceDotNet.Forced)
#endif
            {
                return new AsyncIO.DotNet.CompletionPort();
            }
            else
            {
                return new AsyncIO.Windows.CompletionPort();                
            }                   
        }

        public abstract void Dispose();

        public abstract bool GetQueuedCompletionStatus(int timeout, out CompletionStatus completionStatus);

        public abstract bool GetMultipleQueuedCompletionStatus(int timeout, CompletionStatus[] completionStatuses,
            out int removed);

        public abstract void AssociateSocket(AsyncSocket socket, object state = null);

        public abstract void Signal(object state);
    }
}
