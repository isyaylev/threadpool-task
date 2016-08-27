using System;
using System.Threading;

namespace ThreadPoolTask
{
    /// <summary>
    /// Базовый класс для потоков
    /// </summary>
    internal abstract class ThreadBase
    {
        protected bool isDisposed;

        protected Thread managedThread;

        protected CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Создаёт поток, но не запускает его
        /// </summary>
        public ThreadBase()
        {
            managedThread = new Thread(DoLoop);
        }

        /// <summary>
        /// Просит остановиться
        /// </summary>
        public void Cancel()
        {
            cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Метод, в котором работает поток
        /// </summary>
        protected abstract void DoLoop();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// у всех наследников в конце концов вызовется этот дестрктор
        /// </summary>
        ~ThreadBase()
        {
            Dispose(false);
        }

        public virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // по хорошему - освобождаем managed ресурсы
                    Cancel();

                    // token устроен так, что так можно
                    cancellationTokenSource.Dispose();
                }
                else
                {
                    // по плохому
                    //
 
                    // обязательно остановить managedThread, иначе мы про него просто забудем
                    // Thread.Abort - конечно же зло, но здесь - приемлемое
                    if (ThreadHelper.ThreadShouldBeAborted(managedThread))
                        managedThread.Abort();
                }

                isDisposed = true;
            }
        }

    }
}
