using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ThreadPoolTask
{
    /// <summary>
    /// Содержит коллекцию потоков.
    /// Поддерживает количестко потоков не менее и не более определённого количества.
    /// При Dispose ожидает выполнения всех потоков (и работающих, и останавливающихся).
    /// </summary>
    internal class ThreadCollection : IDisposable
    {
        private bool isDisposed;

        /// <summary>
        /// Запрет изменения колекций
        /// </summary>
        private bool isDisposing;

        private SimpleThreadPoolSettings settings;

        /// <summary>
        /// Для защиты удаления добавления задачи задачи - используется вместо lock
        /// </summary>
        private SemaphoreSlim modificationSemaphore;

        /// <summary>
        /// Набор рабочих потоков.
        /// </summary>
        /// <remarks>Кстати не обязательно использовать конкурентную версию</remarks>
        private ConcurrentStack<WorkingThread> workingThreads;

        /// <summary>
        /// Набор потоков, которых мы уже попросили остановиться, но которые так и не закончили свою работу
        /// </summary>
        private List<WorkingThread> cancellingThreads = new List<WorkingThread>();

        public ThreadCollection(SimpleThreadPoolSettings settings)
        {
            settings.Check();

            this.settings = settings;

            modificationSemaphore = new SemaphoreSlim(1, 1);

            workingThreads = new ConcurrentStack<WorkingThread>();
        }

        /// <summary>
        /// Пытается добавить потоки.
        /// </summary>
        /// <param name="count">количество добавляемых потоков</param>
        /// <param name="threadsConstructor">инициализатор новых потоков, будет вызван только если добавление возможно</param>
        /// <returns>false если операция не возможна</returns>
        public bool TryAddThreads(int count, Func<WorkingThread[]> threadsConstructor)
        {
            if (isDisposing)
                return false;

            try
            {
                modificationSemaphore.Wait();

                if (workingThreads.Count + count > settings.MaxWorkingThreadsCount)
                    return false;

                // мы верим на слово, что количество потоков окажется действительно таким каким нам обещали
                var threads = threadsConstructor();
                workingThreads.PushRange(threads);

                return true;
            }
            finally
            {
                modificationSemaphore.Release();
            }
        }

        /// <summary>
        /// Пытается удалить поток - делает это в два этапа (потому как нельзя пользоваться стандартным ThreadPool),
        /// и следит что бы количество потоков не опустилось ниже определённого
        /// </summary>
        /// <returns>false если операция не возможна</returns>
        public bool TryRemoveThread()
        {
            // Сложность метода заключается вот в чём.
            // Нам нужно попросить поток остановиться, дождаться его остановки, и только после этого потерять 
            // ссылку на него (а в будущем - может быть вызов метода Dispose). 
            // При завершении ThreadPool-а мы должны дожидаться окончания всех потоков, в т.ч.
            // и тех, которых мы уже просили остановиться. 
            // Если бы можно было использовать ThreadPool - существовала бы масса вариантов решения задачи,
            // например мне бы хватило ThreadPool.RegisterSingleWait. Но без этого приходится тяжко.
            // 

            if (isDisposing)
                return false;

            try
            {
                // Из-за ограничения на мин. размер колекции мы вынуждены поместить удаление в критическую секцию.
                modificationSemaphore.Wait();

                if (workingThreads.Count <= settings.MinWorkingThreadsCount)
                    return false;

                WorkingThread thread;

                // Этот приём обеспечивает непрерываемость блока в случае ThreadAbortException
                // Нам ни за что нельзя потерять ссылку на WorkingThread, в общем случае мы не знаем в каком потоке
                // будет работать этот метод, а TAE может испортить нам жизнь.
                try { }
                finally
                {
                    workingThreads.TryPop(out thread);

                    if (thread != null)
                    {
                        // держим ссылку на поток в дополнительной коллекции
                        cancellingThreads.Add(thread);

                        // Нельзя использовать ThreadPool, поэтому приходится выкручиваться событием.
                        // Иначе я бы использовал RegisterSingleWait или простенький континьюейшен
                        thread.Complete += ReleaseThread;

                        // просим поток остановиться - он доработает текущую задачу и умрёт
                        // в конце его работы, он вызовет событие Completion, в котором мы окончательно его забудем
                        thread.Cancel();
                    }
                }

                return thread != null;
            }
            finally
            {
                modificationSemaphore.Release();
            }
        }

        private void ReleaseThread(object sender, WorkingThread.CompletionEventArgs e)
        {
            if (isDisposing)
                return;

            try
            {
                modificationSemaphore.Wait();

                var thread = (WorkingThread)sender;

                cancellingThreads.Remove(thread);

                thread.Dispose();
            }
            finally
            {
                modificationSemaphore.Release();
            }
        }

        public bool TryGetThreadsCount(out int activeThreadsCount, out int cancellingThreadsCount)
        {
            if (isDisposing)
            {
                activeThreadsCount = cancellingThreadsCount = default(int);
                return false;
            }

            try
            {
                modificationSemaphore.Wait();

                activeThreadsCount = workingThreads.Count;
                cancellingThreadsCount = cancellingThreads.Count;

                return true;
            }
            finally
            {
                modificationSemaphore.Release();
            }
        }        

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Внутри ожидаем выполнения всех потоков
        /// </summary>
        /// <param name="disposing"></param>
        public virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                // запрещаем свершится новым модификациям
                isDisposing = true;

                // теперь ожидаем окончания всех текущих модификаций
                modificationSemaphore.Wait();

                modificationSemaphore.Dispose();

                // по задаче мы должны дождаться выполнения всех потоков - и тех, которые всё ещё разгребают очередь,
                // и тех, которых мы уже попросили остановиться, но которые может быть ещё работают.
                var threads = workingThreads.Concat(cancellingThreads).ToArray();

                // вот здесь самое слабое место - мы дожидаемся выполнения остальных задач
                foreach (var thread in threads)
                {
                    thread.Join(settings.WaitOnDisposeTimeout);

                    thread.Dispose();
                }

                isDisposed = true;
            }
        }
    }
}
