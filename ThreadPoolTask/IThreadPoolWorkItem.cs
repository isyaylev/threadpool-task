using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPoolTask
{
    /// <summary>
    /// Элемент в очереди задач на обработку.
    /// </summary>
    internal interface IThreadPoolWorkItem
    {
        void ExecuteWorkItem();
    }
}
