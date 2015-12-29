using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPoolTask.Performance
{
    /// <summary>
    /// Агрегирует и выполняет стратегии управления производительностью.
    /// </summary>
    /// <remarks>
    /// Сам собирает метрики производительности и принимает решение об увеличении\уменьшении количества потоков.
    /// Это не очень хороший патерн и подходит только для простого случая, т.к. наблюдаемый объект должен предоставлять 
    /// интерфейс в угоду этому наблюдателю.
    /// </remarks>
    internal class PerformanceBalancer
    {
        private IThreadCollection threadCollection;

        private PerformanceBalanceSettings settings;

        private IPerformanceBalancerStrategy[] strategies;

        private PerformanceData performanceData = new PerformanceData();

        private IWorkItemCollection workItemCollection;

        public PerformanceBalancer(IThreadCollection threadCollection, IWorkItemCollection workItemCollection, PerformanceBalanceSettings settings)
        {
            settings.Check();

            this.threadCollection = threadCollection;

            this.workItemCollection = workItemCollection;

            this.strategies = (settings.PerformanceBalancerStrategies ?? new List<IPerformanceBalancerStrategy>()).ToArray();

            this.settings = settings;
        }

        /// <summary>
        /// Спрашивает совет у стратегий и выполняет действие
        /// </summary>
        public virtual void DoAction()
        {
            var dataToAnalyse = CollectData();
            foreach (var strategy in this.strategies)
            {
                var action = strategy.GetAction(dataToAnalyse);

                switch (action)
                {
                    case ActionToPerform.AddThreads:
                        threadCollection.TryAddThreads(settings.ThreadsAddCount);
                        performanceData.LastThreadAddedTry = DateTime.Now;
                        break;
                    case ActionToPerform.RemoveThreads:
                        threadCollection.TryRemoveThread();
                        performanceData.LastThreadRemoveTry = DateTime.Now;
                        break;
                }
            }
        }

        /// <summary>
        /// Подготавливает показатели производительности
        /// </summary>
        /// <returns></returns>
        protected virtual PerformanceData CollectData()
        {
            performanceData.QueueLengths.Enqueue(workItemCollection.QueuedItemsCount);

            if (performanceData.QueueLengths.Count > settings.MaxPerformanceDataLength)
                performanceData.QueueLengths.Dequeue();

            return performanceData;
        }
    }
}
