using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPoolTask
{
    /// <summary>
    /// Самый простой случай, который не предполагает захвата ExecutionContext.
    /// Использование этого класса резко сужает применимость пула потоков, 
    /// зато позволяет не морочится с SecuritySafeCritical и SecurityCritical.
    /// Кроме того, проблемы захвата ExecutionContext описаны в пояснительной записке.
    /// Этот класс - по сути обёртка над Action, но она нужна для того, что бы WorkingThread не знал бы об конкретном типе делегата
    /// </summary>
    internal class QueueUserWorkItemCallback : IThreadPoolWorkItem
    {
        static QueueUserWorkItemCallback() { }

        private Action callback;

        public QueueUserWorkItemCallback(Action waitCallback)
        {
            callback = waitCallback;
        }

        void IThreadPoolWorkItem.ExecuteWorkItem()
        {
            // теряем ссылку на callback, что бы а) получить исключение при повторном вызове метода б) не мешать GC при сборке
            // возможного замыкания в callback
            var cb = callback;
            callback = null;
            cb();
        }
    }
}
