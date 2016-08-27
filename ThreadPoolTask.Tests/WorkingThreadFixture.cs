using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ThreadPoolTask.Tests
{
    /// <summary>
    /// Тесты для WorkingThread
    /// </summary>
    [TestClass]
    public class WorkingThreadFixture
    {
        private BlockingCollection<int> queue;

        WorkItemDispatcherMock dispatcher;

        WorkingThread thread;

        bool isThreadCompleted;

        Exception threadException;

        private object unhandledException;

        private void SetupTest(Action<int> action)
        {
            queue = new BlockingCollection<int>();

            dispatcher = new WorkItemDispatcherMock(queue, action);
            thread = new WorkingThread(dispatcher);

            isThreadCompleted = false;
            threadException = null;
            thread.Complete += (object sender, WorkingThread.CompletionEventArgs e) =>
            {
                isThreadCompleted = true;
                threadException = e.Exception;
                Console.WriteLine("thread finished, current thread #{0} {1}, ex={2}", Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.Name, threadException);
            };
        }

        private void errorHandler(object sender, UnhandledExceptionEventArgs e)
        {
            unhandledException = e.ExceptionObject;
            Console.WriteLine("unhandled exception " + e.ExceptionObject);
        }

        [TestInitialize]
        public void BeforeEachTest()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += errorHandler;

            unhandledException = null;
        }

        [TestCleanup]
        public void AfterEachTest()
        {
            queue.Dispose();
            thread.Dispose();

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException -= errorHandler;

            Assert.IsTrue(unhandledException == null || unhandledException is ThreadAbortException);
        }

        [TestMethod]
        [Description("Рабочий поток обрабатывает все элементы в правильном порядке")]
        public void CorrectResultTest()
        {
            var resultingList = new ConcurrentBag<int>();
            var expectedList = new List<int>();

            SetupTest((item) => resultingList.Add(item));

            for (var i = 0; i <= 10; i++)
            {
                queue.Add(i);
                expectedList.Add(i);
            }

            queue.CompleteAdding();

            Thread.Sleep(100);

            var finalList = new List<int>(resultingList);
            finalList.Sort();

            Assert.AreEqual(expectedList.Aggregate(string.Empty, (res, i) => res + i), finalList.Aggregate(string.Empty, (res, i) => res + i));
        }

        [TestMethod]
        [Description("При вызове CompleteAdding рабочий поток дообрабатывает сообщения и прекращает работу")]
        public void CompleteAddingTest()
        {
            var resultingList = new ConcurrentBag<int>();

            SetupTest((item) => resultingList.Add(item));

            queue.CompleteAdding();

            Thread.Sleep(100);

            Assert.AreEqual(true, isThreadCompleted);
            Assert.IsNull(threadException);
        }

        [TestMethod]
        [Description("При вызове Cancel рабочий поток доделывает текущую задачу и прекращает работу")]
        public void CancellationTest()
        {
            var resultingList = new ConcurrentBag<int>();

            SetupTest((item) => { Thread.Sleep(500); resultingList.Add(item); });

            queue.Add(1);

            Thread.Sleep(100);

            // вызываем Cancel вместо вызова queue.CompleteAdding();
            thread.Cancel();

            Thread.Sleep(1000);

            Assert.AreEqual(1, resultingList.Single());
            Assert.AreEqual(true, isThreadCompleted);
            Assert.IsNull(threadException);
        }

        [TestMethod]
        public void JoinNormalTest()
        {
            SetupTest((item) => Thread.Sleep(1000));

            queue.Add(1);
            queue.CompleteAdding();

            thread.Join(10000);
            Thread.Sleep(100);

            Assert.AreEqual(true, isThreadCompleted);
            Assert.IsNull(threadException);
        }

        [TestMethod]
        public void JoinWithAbortTest()
        {
            SetupTest((item) => Thread.Sleep(10000));

            queue.Add(1);
            queue.CompleteAdding();

            thread.Join(1000);
            Thread.Sleep(100);

            Assert.AreEqual(true, isThreadCompleted);
            Assert.IsInstanceOfType(threadException, typeof(ThreadAbortException));
        }
    }
}
