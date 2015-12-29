using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPoolTask.Performance
{
    /// <summary>
    /// Базовый класс с показателями производительности
    /// </summary>
    public class PerformanceData
    {
        public PerformanceData()
        {
            QueueLengths = new Queue<int>();

            LastThreadAddedTry = LastThreadRemoveTry = DateTime.Now;
        }

        /// <summary>
        /// История длины очереди
        /// </summary>
        public Queue<int> QueueLengths { get; private set; }

        /// <summary>
        /// Дата последней попытки удалить поток
        /// </summary>
        public DateTime LastThreadRemoveTry { get; set; }

        /// <summary>
        /// Дата последней попытки добавить поток
        /// </summary>
        public DateTime LastThreadAddedTry { get; set; }
    }
}
