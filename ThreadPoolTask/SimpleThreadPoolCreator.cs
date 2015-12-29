using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreadPoolTask.Performance;

namespace ThreadPoolTask
{
    /// <summary>
    /// Фабрика пула
    /// </summary>
    public class SimpleThreadPoolCreator
    {
        public SimpleThreadPoolSettings Settings { get; private set; }

        public PerformanceBalanceSettings PerformanceBalanceSettings { get; private set; }

        public SimpleThreadPoolCreator()
            : this (new SimpleThreadPoolSettings(SimpleThreadPoolSettings.DefaultWorkingThreadsCount), new PerformanceBalanceSettings(1))
        {
        }

        public SimpleThreadPoolCreator(SimpleThreadPoolSettings settings, PerformanceBalanceSettings performanceBalanceSettings)
        {
            Settings = settings;
            PerformanceBalanceSettings = performanceBalanceSettings;
        }

        /// <summary>
        /// Создаёт пул в соответствии с настройками
        /// </summary>
        /// <returns>Новый пул</returns>
        public ISimpleThreadPool Create()
        {
            var threadCollection = new ThreadCollection(Settings);
            var result = new SimpleThreadPool(Settings, threadCollection);

            var strategies = PerformanceBalanceSettings.PerformanceBalancerStrategies ?? new List<IPerformanceBalancerStrategy>();

            if (strategies.Count > 0)
            {
                var balancer = new PerformanceBalancer(result, result, PerformanceBalanceSettings);
                result.SetPerformanceBalancer(balancer, PerformanceBalanceSettings);
            }

            return result;
        }
    }
}
