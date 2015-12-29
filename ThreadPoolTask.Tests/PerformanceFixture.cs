using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThreadPoolTask.Performance;

namespace ThreadPoolTask.Tests
{
    /// <summary>
    /// Тесты для PerformanceBalancer
    /// </summary>
    [TestClass]
    public class PerformanceFixture
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
        [Description("Балансер уменьшает количество потоков, после чего пул продолжает быть работоспособным")]
        public void RemoveThreadsTest()
        {
            var resultingList = new List<int>();
            var expectedList = new List<int>();

            var creator = new SimpleThreadPoolCreator(
                new SimpleThreadPoolSettings(1) { MaxWorkingQueueCapacity = 1 },
                new PerformanceBalanceSettings(1) { 
                    ActionInterval = 100, 
                    PerformanceBalancerStrategies = new List<IPerformanceBalancerStrategy>() 
                    {
                        // последний параметр - как часто можно применять действие (в милисекундах)
                        new PerformanceBalancerSimpleStrategy(2 * Environment.ProcessorCount, 2, 100) 
                    }
                });

            int activeThreadsCount;
            int cancellingThreadsCount;

            using (var threadPool = creator.Create())
            {
                // за 4*0.1 секунды балансер удалит 3 потока, после чего будет продолжать удалять (но ему не дадут).
                // в результате останется один рабочий поток.
                for (var i = 0; i < 2; i++)
                {
                    var local = i;
                    threadPool.Add(() => { resultingList.Add(local); });

                    expectedList.Add(i);

                    Thread.Sleep(1000);
                }

                var tryResult = ((IThreadCollection)threadPool).TryGetThreadsCount(out activeThreadsCount, out cancellingThreadsCount);

                Assert.IsTrue(tryResult);
            }

            Assert.AreEqual(1, activeThreadsCount);
            Assert.AreEqual(0, cancellingThreadsCount);

            // если бы dispose не дождался завершения задач в очереди, то в finalList не попал бы результат
            var finalList = new List<int>(resultingList);
            finalList.Sort();
            Assert.AreEqual(expectedList.Aggregate(string.Empty, (res, i) => res + i), finalList.Aggregate(string.Empty, (res, i) => res + i));
        }

        [TestMethod]
        [Description("Балансер увеличит количество потоков, после чего пул продолжает быть работоспособным")]
        public void AddThreadsTest()
        {
            var resultingList = new List<int>();
            var expectedList = new List<int>();

            var creator = new SimpleThreadPoolCreator(
                new SimpleThreadPoolSettings(1) { StartWorkingThreadsCount = 1, MaxWorkingThreadsCount = 2 },
                new PerformanceBalanceSettings(1) {
                    ActionInterval = 100,
                    PerformanceBalancerStrategies = new List<IPerformanceBalancerStrategy>() 
                    {
                        // первый параметр - сколько сообщений в очереди должно быть, что бы добавить новый поток
                        // последний параметр - как часто можно применять действие (в милисекундах)
                        new PerformanceBalancerSimpleStrategy(1, 2, 100) 
                    }
                });

            int activeThreadsCount;
            int cancellingThreadsCount;

            using (var threadPool = creator.Create())
            {
                // первая задача - что бы на долго занять первы поток
                threadPool.Add(() =>
                {
                    Thread.Sleep(1000);
                    resultingList.Add(-1);
                });

                expectedList.Add(-1);

                // балансер за 0.4 секунды 3 раза попробует добавить поток и в первый раз у него это получится.
                // следующие его попытки добавить поток не увенчаются успехом
                for (var i = 0; i < 4; i++)
                {
                    var local = i;
                    threadPool.Add(() =>
                    {
                        Thread.Sleep(100);
                        resultingList.Add(local);
                    });

                    expectedList.Add(i);
                }

                Thread.Sleep(1000);

                var tryResult = ((IThreadCollection)threadPool).TryGetThreadsCount(out activeThreadsCount, out cancellingThreadsCount);

                Assert.IsTrue(tryResult);
            }

            Assert.AreEqual(2, activeThreadsCount);
            Assert.AreEqual(0, cancellingThreadsCount);

            // если бы dispose не дождался завершения задач в очереди, то в finalList не попал бы результат
            var finalList = new List<int>(resultingList);
            finalList.Sort();
            Assert.AreEqual(expectedList.Aggregate(string.Empty, (res, i) => res + i), finalList.Aggregate(string.Empty, (res, i) => res + i));
        }
    }
}
