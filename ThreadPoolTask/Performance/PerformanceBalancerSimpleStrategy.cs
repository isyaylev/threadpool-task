using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPoolTask.Performance
{
    /// <summary>
    /// Простая стратегия с просто эвристикой
    /// </summary>
    public class PerformanceBalancerSimpleStrategy : IPerformanceBalancerStrategy
    {
        private int addThreadThreshold;

        private int removeThreadThreshold;

        private int actionDelay;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="addThreadThreshold">Порог - количество задач в очереди, после которого нужно создавать новый поток</param>
        /// <param name="removeThreadThreshold">Порог - если количество задач в очереди на протяжении истории было меньше этого числа, то нужно убрать один поток</param>
        /// <param name="actionDelay">Совет не будет даваться чаще, чем это время в милисекундах</param>
        public PerformanceBalancerSimpleStrategy(int addThreadThreshold, int removeThreadThreshold, int actionDelay)
        {
            this.addThreadThreshold = addThreadThreshold;
            this.removeThreadThreshold = removeThreadThreshold;
            this.actionDelay = actionDelay;
        }

        public ActionToPerform GetAction(PerformanceData dataToAnalyse)
        {
            if (dataToAnalyse.LastThreadAddedTry.AddMilliseconds(actionDelay) > DateTime.Now)
                return ActionToPerform.DoNothing;

            if (dataToAnalyse.QueueLengths.LastOrDefault() > addThreadThreshold)
                return ActionToPerform.AddThreads;

            if (dataToAnalyse.QueueLengths.All(length => length < removeThreadThreshold))
                return ActionToPerform.RemoveThreads;

            return ActionToPerform.DoNothing;
        }
    }
}
