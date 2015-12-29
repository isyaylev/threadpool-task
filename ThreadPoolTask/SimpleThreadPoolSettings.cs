using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolTask
{
    /// <summary>
    /// Настройки пула
    /// </summary>
    public struct SimpleThreadPoolSettings
    {
        public static int DefaultWorkingThreadsCount { get { return Environment.ProcessorCount; } }

        /// <summary>
        /// Ограничение на длину очереди
        /// </summary>
        public int? MaxWorkingQueueCapacity { get; set; }

        /// <summary>
        /// Минимальное количество рабочих потоков
        /// </summary>
        public int MinWorkingThreadsCount { get; set; }

        /// <summary>
        /// Максимальное количество рабочих потоков
        /// </summary>
        public int MaxWorkingThreadsCount { get; set; }

        /// <summary>
        /// Стартовое количетсво рабочих потоков
        /// </summary>
        public int StartWorkingThreadsCount { get; set; }

        /// <summary>
        /// Таймаут ожидания завершения рабочего потока "по хорошему"
        /// </summary>
        public int WaitOnDisposeTimeout { get; set; }

        public SimpleThreadPoolSettings(int maximalWorkingThreadsCount)
            : this()
        {
            MaxWorkingThreadsCount = StartWorkingThreadsCount = maximalWorkingThreadsCount;

            MinWorkingThreadsCount = 1;

            WaitOnDisposeTimeout = 5000;
        }

        /// <summary>
        /// Валидирует настройки и в случае ошибки выбрасывает ArgumentException
        /// </summary>
        public void Check()
        {
            if (StartWorkingThreadsCount < 1)
                throw new ArgumentException("StartWorkingThreadsCount");

            if (MaxWorkingQueueCapacity < 1)
                throw new ArgumentException("MaxWorkingQueueCapacity");

            if (MaxWorkingThreadsCount < 1)
                throw new ArgumentException("MaxWorkingThreadsCount");

            if (MaxWorkingThreadsCount > 1024)
                throw new ArgumentException("MaxWorkingThreadsCount");

            if (MinWorkingThreadsCount < 1)
                throw new ArgumentException("MinWorkingThreadsCount");

            if (WaitOnDisposeTimeout < Timeout.Infinite)
                throw new ArgumentException("WaitOnDisposeTimeout");

            if (MaxWorkingQueueCapacity != null && MaxWorkingQueueCapacity < 1)
                throw new ArgumentException("MaxWorkingQueueCapacity");
        }
    }
}
