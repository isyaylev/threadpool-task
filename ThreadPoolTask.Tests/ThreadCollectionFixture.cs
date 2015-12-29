using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolTask.Tests
{
    /// <summary>
    /// Тесты для ThreadCollection 
    /// </summary>
    [TestClass]
    public class ThreadCollectionFixture
    {
        private BlockingCollection<int> queue;

        WorkItemDispatcherMock dispatcher;

        ThreadCollection threadCollection;

        private object unhandledException;

        private void SetupTest(Action<int> action)
        {
            queue = new BlockingCollection<int>();

            dispatcher = new WorkItemDispatcherMock(queue, action);
        }

        private bool AddThreads(int count)
        {
            return threadCollection.TryAddThreads(
                    count,
                    () => Enumerable.Repeat(0, count).Select(i => new WorkingThread(dispatcher)).ToArray());
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

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException -= errorHandler;

            Assert.IsTrue(unhandledException == null || unhandledException is ThreadAbortException);
        }

        [TestMethod]
        [Description("КОнкурентные потоки опустошают очередь")]
        public void CorrectResultTest()
        {
            var resultingList = new List<int>();
            var expectedList = new List<int>();

            SetupTest((item) => { resultingList.Add(item); Thread.Sleep(100); });

            for (var i = 0; i < 100; i++)
            {
                queue.Add(i);
                expectedList.Add(i);
            }

            using (threadCollection = new ThreadCollection(new SimpleThreadPoolSettings(10)))
            {
                var addResult = AddThreads(10);

                queue.CompleteAdding();

                Assert.IsTrue(addResult);
            }

            resultingList.Sort();
            Assert.AreEqual(expectedList.Aggregate(string.Empty, (res, i) => res + i), resultingList.Aggregate(string.Empty, (res, i) => res + i));
        }

        [TestMethod]
        [Description("Нельзя добавить потоков больше определённого количества")]
        public void CannotAddThreadTest()
        {
            var resultingList = new List<int>();
            SetupTest((item) => { resultingList.Add(item); });

            using (threadCollection = new ThreadCollection(new SimpleThreadPoolSettings(1)))
            {
                var addResult = AddThreads(10);

                queue.CompleteAdding();

                Assert.IsFalse(addResult);
            }
        }

        [TestMethod]
        [Description("Нельзя удалить потоков больше определённого количества")]
        public void CannotRemoveThreadTest()
        {
            var resultingList = new List<int>();
            SetupTest((item) => { resultingList.Add(item); });

            using (threadCollection = new ThreadCollection(new SimpleThreadPoolSettings(1)))
            {
                var addResult = AddThreads(1);

                Assert.IsTrue(addResult);

                var removeResult = threadCollection.TryRemoveThread();

                queue.CompleteAdding();

                Assert.IsFalse(removeResult);
            }
        }

        [TestMethod]
        [Description("Удаляем работающие потоки, и следим за тем, что бы их работа выполнилась до выхода из Dispose")]
        public void RemoveThreadTest()
        {
            var resultingList = new List<int>();
            var expectedList = new List<int>();

            SetupTest((item) => { Thread.Sleep(1000); resultingList.Add(item); });

            using (threadCollection = new ThreadCollection(new SimpleThreadPoolSettings(2)))
            {
                var addResult = AddThreads(2);

                Assert.IsTrue(addResult);

                for (var i = 0; i < 2; i++)
                {
                    queue.Add(i);
                    expectedList.Add(i);
                    Thread.Sleep(500);
                }

                queue.CompleteAdding();

                // просим отменится второй поток, который сейчас работает
                var removeResult = threadCollection.TryRemoveThread();

                Assert.IsTrue(removeResult);
            }

            // если бы dispose не дождался тот второй отменённый поток, то в finalList не попал бы результат
            var finalList = new List<int>(resultingList);
            finalList.Sort();
            Assert.AreEqual(expectedList.Aggregate(string.Empty, (res, i) => res + i), finalList.Aggregate(string.Empty, (res, i) => res + i));
        }
    }
}
