using System;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using ThreadPoolTask.Performance;

namespace ThreadPoolTask
{
    /// <summary>
    /// Пул потоков
    /// </summary>
    public interface ISimpleThreadPool: IDisposable
    {
        /// <summary>
        /// регистрация новой задачи. 
        /// </summary>
        /// <remarks>Генерирует InvalidOperationException, если в текущий момент не допускается добавление новой задачи.</remarks>
        /// <param name="action">задача для выполнения в пуле</param>
        void Add(Action action);

        /// <summary>
        /// попытка регистрация новой задачи. 
        /// </summary>
        /// <param name="action">задача для выполнения в пуле</param>
        /// <returns>Возвращает false, если в текущий момент не допускается добавление новой задачи.</returns>
        bool TryAdd(Action action);
    }

    /// <summary>
    /// Определитель следущей задачи на обработку
    /// </summary>
    internal interface IWorkItemDispatcher
    {
        /// <summary>
        /// Отправить следующую задачу на обработку
        /// </summary>
        /// <param name="item">задача</param>
        /// <param name="cancellationToken">токен отмены</param>
        /// <returns>false если выимку задач нужно прекратить</returns>
        bool Dispatch(out IThreadPoolWorkItem item, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Колекция потоков
    /// </summary>
    public interface IThreadCollection
    {
        /// <summary>
        /// Добавить указанное количество потоков
        /// </summary>
        /// <param name="count">Количество потоков</param>
        bool TryAddThreads(int count);

        /// <summary>
        /// Удалить один поток
        /// </summary>
        bool TryRemoveThread();

        /// <summary>
        /// Попытка получить количество потоков
        /// </summary>
        /// <param name="activeThreadsCount">количетсво рабочих потоков</param>
        /// <param name="cancellingThreadsCount">количество отменённых, но всё ещё работающих потоков</param>
        bool TryGetThreadsCount(out int activeThreadsCount, out int cancellingThreadsCount);
    }

    /// <summary>
    /// Колекция задач на обработку
    /// </summary>
    public interface IWorkItemCollection
    {
        /// <summary>
        /// Текущее количество задач в очереди
        /// </summary>
        int QueuedItemsCount { get; }
    }

    /// <summary>
    /// Реализация пула потоков
    /// </summary>
    /// <remarks>
    /// Внутри принципиально не имеет использований стандартного пула потоков. 
    /// Является фасадом для колекции задач, колекции потоков, диспатчера задач и балансера производительности.
    /// Использует особенности BlockingCollection для реализации конкурентного потребления из очереди - это кастомизировать нельзя.
    /// PerformanceBalancer умеет автоматически изменять количество рабочих потоков.
    /// Использует ThreadCollection для управления потоками.
    /// В методе Dispose вызывает ThreadCollection.Dispose, который дожидается выполнения всех когда-либо добавленных задач, 
    /// что может вызвать performance hit.
    /// </remarks>
    internal class SimpleThreadPool : ISimpleThreadPool, IWorkItemDispatcher, IThreadCollection, IWorkItemCollection, IDisposable
    {
        private bool isDisposed;

        private SimpleThreadPoolSettings settings;

        private BlockingCollection<IThreadPoolWorkItem> blockingCollection;

        private ThreadCollection threadCollection;

        private PerformanceBalancerThread balancerThread;

        internal SimpleThreadPool(SimpleThreadPoolSettings settings, ThreadCollection threadCollection)
        {
            if (threadCollection == null)
                throw new ArgumentException("threadCollection");

            settings.Check();

            this.settings = settings;

            if (this.settings.MaxWorkingQueueCapacity == null)
                blockingCollection = new BlockingCollection<IThreadPoolWorkItem>();
            else
                blockingCollection = new BlockingCollection<IThreadPoolWorkItem>(this.settings.MaxWorkingQueueCapacity.Value);

            this.threadCollection = threadCollection;

            TryAddThreads(settings.StartWorkingThreadsCount);
        }

        /// <inheritdoc/>
        public void Add(Action action)
        {
            // blockingCollection при попытке добавить задач сверх boundedCapacity подвешивает поток, а мы должны бросать исключние
            if (action == null)
            {
                throw new ArgumentNullException("WaitCallback");
            }

            var result = TryAdd(action);

            if (!result)
                throw new InvalidOperationException("Попытка добавить задачу сверх заданного boundedCapacity");
        }

        /// <inheritdoc/>
        public bool TryAdd(Action action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            var callback = new QueueUserWorkItemCallback(action);

            return blockingCollection.TryAdd(callback);
        }

        /// <inheritdoc/>
        bool IWorkItemDispatcher.Dispatch(out IThreadPoolWorkItem item, CancellationToken cancellationToken)
        {
            // с такими параметрами TryTake вернёт false только если операцию отменили.
            return blockingCollection.TryTake(out item, Timeout.Infinite, cancellationToken);
        }

        /// <summary>
        /// Устанавливает новый балансер, заставляет завершится старый.
        /// </summary>
        /// <remarks>
        /// Балансер сделан частью трид-пула (композиция) для того что бы время жизни балансера не превысила времени жизни
        /// самого пула. 
        /// Т.к. это особенность данной реализации - я и не стал выносить его под интерфейс
        /// </remarks>
        /// <param name="balancer">Новый балансер</param>
        internal void SetPerformanceBalancer(PerformanceBalancer balancer, PerformanceBalanceSettings settings)
        {
            CancelPerformanceBalancer();

            if (balancer != null)
            {
                balancerThread = new PerformanceBalancerThread(balancer, settings);
            }
        }

        /// <summary>
        /// Высвобождает (т.е. останавливает) поток балансера
        /// </summary>
        private void CancelPerformanceBalancer()
        {
            if (balancerThread != null)
            {
                balancerThread.Dispose();
                balancerThread = null;
            }
        }

        /// <inheritdoc/>
        public int QueuedItemsCount
        {
            get { return blockingCollection.Count; }
        }

        /// <inheritdoc/>
        public bool TryAddThreads(int count)
        {
            if (count < 1)
                throw new ArgumentException("count");

            return threadCollection.TryAddThreads(count, () =>
                Enumerable.Repeat(0, count)
                    .Select(i => new WorkingThread(this))
                    .ToArray());
        }

        /// <inheritdoc/>
        public bool TryRemoveThread()
        {
            return threadCollection.TryRemoveThread();
        }

        /// <inheritdoc/>
        public bool TryGetThreadsCount(out int activeThreadsCount, out int cancellingThreadsCount)
        {
            return threadCollection.TryGetThreadsCount(out activeThreadsCount, out cancellingThreadsCount);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Высвобождает занятые ресурсы. 
        /// В частности, завершает добавление задач, останавливает балансер, ожидает завершения рабочих потоков
        /// </summary>
        /// <param name="disposing"></param>
        public virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                // запрещаем добавление элементов в очередь
                blockingCollection.CompleteAdding();

                // останавливаем балансер
                CancelPerformanceBalancer();

                // это приведёт к ожиданию завершения работы всеми потоками
                threadCollection.Dispose();

                blockingCollection.Dispose();

                isDisposed = true;
            }
        }
    }    
}
