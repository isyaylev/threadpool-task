using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreadPoolTask.Performance;

namespace ThreadPoolTask.Performance
{
    /// <summary>
    /// Настройки балансировщика производительности
    /// </summary>
    public struct PerformanceBalanceSettings
    {
        public List<IPerformanceBalancerStrategy> PerformanceBalancerStrategies { get; set; }

        public int MaxPerformanceDataLength { get; set; }

        public int ThreadsAddCount { get; set; }

        public int ActionInterval { get; set; }

        public PerformanceBalanceSettings(int threadsAddCount)
            : this()
        {
            MaxPerformanceDataLength = 10;

            ThreadsAddCount = threadsAddCount;

            ActionInterval = 1000;

            PerformanceBalancerStrategies = new List<IPerformanceBalancerStrategy>() 
            {
                // эти три константы кастомизировать не дам
                new PerformanceBalancerSimpleStrategy(2 * Environment.ProcessorCount, 2, 2000) 
            };
        }

        /// <summary>
        /// Валидирует настройки и в случае ошибки выбрасывает ArgumentException
        /// </summary>
        public void Check()
        {
            if (MaxPerformanceDataLength < 1)
                throw new ArgumentException("MaxPerformanceDataLength");

            if (ThreadsAddCount > 10 || ThreadsAddCount < 1)
                throw new ArgumentException("ThreadsAddCount");

            if (ActionInterval < 100)
                throw new ArgumentException("ActionInterval");
        }
    }
}
