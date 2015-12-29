using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolTask
{
    /// <summary>
    /// Рабочий поток, который берёт работу у своего хозяина (IWorkItemDispatcher)
    /// </summary>
    internal class WorkingThread : ThreadBase, IDisposable
    {
        private static int counter;

        /// <summary>
        /// это тот, кто будет давать нам новую задачу
        /// </summary>
        private IWorkItemDispatcher dispatcher;

        private bool isProcessing;

        /// <summary>
        /// Пользовательская задача выполняется
        /// </summary>
        public bool IsProcessing
        {
            get
            {
                return isProcessing;
            }
        }

        /// <summary>
        /// Событие, срабатывает при любом завершении потока.
        /// Используется как континьюешен
        /// </summary>
        public event EventHandler<CompletionEventArgs> Complete;

        public WorkingThread(IWorkItemDispatcher dispatcher)
            : base()
        {
            this.dispatcher = dispatcher;

            managedThread.Name = "SimpleWorkingThread#" + Interlocked.Increment(ref counter);

            managedThread.Start();
        }

        /// <summary>
        /// Ждет завершения потока указанное время, если не дождётся - вызывает ему Abort
        /// </summary>
        /// <param name="millisecondsTimeout">время для ожидания</param>
        public void Join(int millisecondsTimeout)
        {
            if (!managedThread.Join(millisecondsTimeout))
            {
                managedThread.Abort();
            }
        }

        /// <summary>
        /// Главный цикл
        /// </summary>
        protected override void DoLoop()
        {
            IThreadPoolWorkItem workItem = null;
            Exception exception = null;

            try
            {
                while (!cancellationTokenSource.IsCancellationRequested &&
                    dispatcher.Dispatch(out workItem, cancellationTokenSource.Token))
                {
                    isProcessing = true;

                    workItem.ExecuteWorkItem();

                    isProcessing = false;
                }
            }
            catch (OperationCanceledException ex)
            {
                exception = ex;
                // гасим исключение - просто выходим из метода
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                FireCompletion(exception);
            }
        }

        protected virtual void FireCompletion(Exception ex)
        {
            var handler = Complete;
            if (handler != null)
                handler(this, new CompletionEventArgs() { Exception = ex });
        }

        public virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (!disposing)
                {
                    // по плохому
                    //
                    // Complete - в данном случае он используется вместо continuation
                    // При отмене потока нужно не допустить вызова континьюейшена.
                    Complete = null;
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Параметры события Completion
        /// </summary>
        public class CompletionEventArgs : EventArgs
        {
            public Exception Exception { get; set; }
        }
    }
}
