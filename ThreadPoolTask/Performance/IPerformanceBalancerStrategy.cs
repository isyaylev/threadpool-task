using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPoolTask.Performance
{
    /// <summary>
    /// Стратегия, дающая советы - как улучшить производительность
    /// </summary>
    public interface IPerformanceBalancerStrategy
    {
        ActionToPerform GetAction(PerformanceData dataToAnalyse);
    }

    /// <summary>
    /// Действия, которые можно совершить для улучшения производительности
    /// </summary>
    public enum ActionToPerform
    {
        DoNothing, AddThreads, RemoveThreads
    }
}
