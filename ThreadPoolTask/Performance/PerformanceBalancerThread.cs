using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolTask.Performance
{
    /// <summary>
    /// Поток, осуществляющий настройку производительности.
    /// Сделан очень просто и не надёжно - в случае любого исключения перестаёт работать.
    /// Если было можно использовать ThreadPool - я бы пользовался Timer, но увы приходится создавать свой "сонный" поток.
    /// </summary>
    internal class PerformanceBalancerThread : ThreadBase, IDisposable
    {
        private PerformanceBalancer performanceBalancer;

        private PerformanceBalanceSettings settings;

        public PerformanceBalancerThread(PerformanceBalancer performanceBalancer, PerformanceBalanceSettings settings)
            : base()
        {
            this.settings = settings;

            this.performanceBalancer = performanceBalancer;

            managedThread.IsBackground = true;
            managedThread.Name = "PerformanceBalancerThread";

            managedThread.Start();
        }

        /// <summary>
        /// Главный цикл
        /// </summary>
        protected override void DoLoop()
        {
            Thread.Sleep(settings.ActionInterval);

            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    performanceBalancer.DoAction();

                    // я не могу использовать Timer из за того, что нельзя использовать ThreadPool
                    // поэтому пользуюсь Thread.Sleep
                    Thread.Sleep(settings.ActionInterval);
                }
            }
            finally
            {
                // Теряем ссылку на объект, что бы не мешать GC. Между окончанием потока и высвобождением стека может пройти какое-то время.
                performanceBalancer = null;
            }
        }
    }
}
