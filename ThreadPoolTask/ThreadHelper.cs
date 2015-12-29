using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolTask
{
    internal static class ThreadHelper
    {
        public static bool ThreadShouldBeAborted(Thread managedThread)
        {
            return (managedThread.ThreadState & ThreadState.StopRequested | ThreadState.Unstarted | ThreadState.Stopped | ThreadState.Aborted | ThreadState.AbortRequested) == 0;
        }
    }
}
