using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolTask.Tests
{
    public class WorkItemDispatcherMock : IWorkItemDispatcher
    {
        private BlockingCollection<int> queue;

        private Action<int> action;

        public WorkItemDispatcherMock(BlockingCollection<int> queue, Action<int> action)
        {
            this.queue = queue;
            this.action = action;
        }

        bool IWorkItemDispatcher.Dispatch(out IThreadPoolWorkItem item, System.Threading.CancellationToken cancellationToken)
        {
            int queueItem;
            var result = queue.TryTake(out queueItem, Timeout.Infinite, cancellationToken);

            item = new QueueUserWorkItemCallback(() =>
            {
                Console.WriteLine("action {0}, current thread #{1} {2}", queueItem, Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.Name);
                action(queueItem);
            });

            return result;
        }
    }
}
