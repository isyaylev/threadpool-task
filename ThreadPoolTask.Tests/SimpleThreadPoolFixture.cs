using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using ThreadPoolTask.Performance;

namespace ThreadPoolTask.Tests
{
    /// <summary>
    /// Тесты для SimpleThreadPool
    /// </summary>
    [TestClass]
    public class SimpleThreadPoolFixture
    {
        private object unhandledException;

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
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException -= errorHandler;

            Assert.IsTrue(unhandledException == null || unhandledException is ThreadAbortException);
        }

        [TestMethod]
        [Description("Рабочий поток обрабатывает все элементы в правильном порядке и дожидается завершение всех потоков")]
        public void CorrectResultTest()
        {
            var resultingList = new List<int>();
            var expectedList = new List<int>();

            using (var threadPool = (new SimpleThreadPoolCreator()).Create())
            {
                for (var i = 0; i < 10; i++)
                {
                    var local = i;
                    threadPool.Add(() =>
                    {
                        Thread.Sleep(100);
                        resultingList.Add(local);
                    });

                    expectedList.Add(i);
                }
            }

            // если бы dispose не дождался завершения задач в очереди, то в finalList не попал бы результат
            var finalList = new List<int>(resultingList);
            finalList.Sort();
            Assert.AreEqual(expectedList.Aggregate(string.Empty, (res, i) => res + i), finalList.Aggregate(string.Empty, (res, i) => res + i));
        }

        [TestMethod]
        [Description("Исключение при попытке привысить лимит очереди")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CannotAddActionTest()
        {
            var creator = new SimpleThreadPoolCreator(
                new SimpleThreadPoolSettings(1) { MaxWorkingQueueCapacity = 1 },
                new PerformanceBalanceSettings(1));

            using (var threadPool = creator.Create())
            {
                // Занимаем первый поток
                threadPool.Add(() => { Thread.Sleep(100); });

                // Добавляем элемент в очередь
                threadPool.Add(() => { Thread.Sleep(100); });

                // Получаем ичключение
                threadPool.Add(() => { Thread.Sleep(100); });                
            }
        }

        [TestMethod]
        [Description("Нет исключения при попытке использовать TryAdd")]
        public void TryAddActionTest()
        {
            var creator = new SimpleThreadPoolCreator(
                new SimpleThreadPoolSettings(1) { MaxWorkingQueueCapacity = 1 },
                new PerformanceBalanceSettings(1));

            using (var threadPool = creator.Create())
            {
                // Занимаем первый поток
                var addResult = threadPool.TryAdd(() => { Thread.Sleep(100); });
                Assert.IsTrue(addResult);

                // Добавляем элемент в очередь
                addResult = threadPool.TryAdd(() => { Thread.Sleep(100); });
                Assert.IsTrue(addResult);

                // Вернётся false
                addResult = threadPool.TryAdd(() => { Thread.Sleep(100); });
                Assert.IsFalse(addResult);
            }
        }
    }
}
