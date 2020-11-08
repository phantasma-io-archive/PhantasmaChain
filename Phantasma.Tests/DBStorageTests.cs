using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.RocksDB;
using Phantasma.Storage;
using System.Threading;
using ConsoleLogger = Phantasma.Core.Log.ConsoleLogger;
using System.Text;
using Phantasma.Storage.Context;
using Phantasma.Numerics;

namespace Phantasma.Tests
{

    [TestClass]
    public class DBStorageTests
    {
        private readonly string path = "./Storage/";
        private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;
        private KeyValueStore<string, string> _testStorage = null;

        [TestInitialize()]
        public void TestInitialize()
        {
            _adapterFactory = _adapterFactory = (name) => { return new DBPartition(new ConsoleLogger(), path + name); };
            _testStorage = new KeyValueStore<string, string>(CreateKeyStoreAdapterTest("test"));
        }

        [ClassCleanup()]
        public static void ClassCleanup()
        {
            if (Directory.Exists(Directory.GetCurrentDirectory()+"/Storage/"))
            {
                Directory.Delete(Directory.GetCurrentDirectory() + "/Storage/", true);
            }
        }

        [TestCleanup()]
        public void TestCleanup()
        {
            _testStorage.Visit((key, _) =>
            {
                _testStorage.Remove(key);
            });
        }

        private IKeyValueStoreAdapter CreateKeyStoreAdapterTest(string name)
        {
            var result = _adapterFactory(name);
            Throw.If(result == null, "keystore adapter factory failed");
            return result;
        }

        [TestMethod]
        public void TestDBStorageSet()
        {
            _testStorage.Set("test1", "Value11");
            string val = _testStorage.Get("test1");
            Assert.IsTrue(val == "Value11");
        }

        [TestMethod]
        public void TestDBStorageRemove()
        {
            _testStorage.Set("test1", "Value11");
            _testStorage.Remove("test1");

            Assert.ThrowsException<Exception>(() =>
            {
                string val = _testStorage.Get("test1");
            });
        }

        [TestMethod]
        public void TestDBStorageContains()
        {
            _testStorage.Set("test1", "Value11");
            bool result = _testStorage.ContainsKey("test1");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TestDBStorageVisit()
        {
            _testStorage.Set("test1", "Value11");
            _testStorage.Set("test2", "Value12");
            _testStorage.Set("test3", "Value13");
            _testStorage.Set("test4", "Value14");

            var keyList = new List<string>();
            var valueList = new List<string>();
            _testStorage.Visit((key, value) =>
            {
                keyList.Add(key);
                valueList.Add(value);
            });

            Assert.IsTrue(keyList.Count == valueList.Count);
        }

        [TestMethod]
        public void TestDBStorageMapClear()
        {
            var storage = new KeyStoreStorage(CreateKeyStoreAdapterTest("test2"));

            var testMapKey = Encoding.UTF8.GetBytes($".test._valueMap");
            var testMapKey2 = Encoding.UTF8.GetBytes($".test2._valueMap");

            var testMap = new StorageMap(testMapKey, storage);
            var testMap2 = new StorageMap(testMapKey2, storage);

            testMap.Set("test1", "Value1");
            testMap.Set("test2", "Value2");
            testMap.Set("test3", "Value3");
            testMap.Set("test4", "Value4");

            testMap2.Set<BigInteger, string>(new BigInteger(1), "Value21");
            testMap2.Set<BigInteger, string>(new BigInteger(2), "Value22");
            testMap2.Set<BigInteger, string>(new BigInteger(3), "Value23");
            testMap2.Set<BigInteger, string>(new BigInteger(4), "Value24");

            var count = 0;
            testMap.Visit<string, string>((key, value) => {
                count++;
            });

            testMap2.Visit<BigInteger, string>((key, value) => {
                count++;

            });

            Assert.AreEqual(count, (int)testMap.Count() + testMap2.Count());

            testMap.Clear();
            testMap2.Clear();

            Assert.IsTrue(testMap.Count() == 0);
            Assert.IsTrue(testMap2.Count() == 0);

            Assert.IsNull(testMap.Get<string,string>("test1"));
            Assert.IsNull(testMap.Get<string,string>("test2"));
            Assert.IsNull(testMap.Get<string,string>("test3"));
            Assert.IsNull(testMap.Get<string,string>("test4"));

            Assert.IsNull(testMap2.Get<BigInteger,string>(new BigInteger(1)));
            Assert.IsNull(testMap2.Get<BigInteger,string>(new BigInteger(2)));
            Assert.IsNull(testMap2.Get<BigInteger,string>(new BigInteger(3)));
            Assert.IsNull(testMap2.Get<BigInteger,string>(new BigInteger(4)));
        }

        [TestMethod]
        public void TestDBStorageVisitMap()
        {
            var storage = new KeyStoreStorage(CreateKeyStoreAdapterTest("test2"));

            var testMapKey = Encoding.UTF8.GetBytes($".test._valueMap");
            var testMapKey2 = Encoding.UTF8.GetBytes($".test2._valueMap");

            var testMap = new StorageMap(testMapKey, storage);
            var testMap2 = new StorageMap(testMapKey2, storage);

            testMap.Set("test1", "Value1");
            testMap.Set("test2", "Value2");
            testMap.Set("test3", "Value3");
            testMap.Set("test4", "Value4");

            testMap2.Set<BigInteger, string>(new BigInteger(1), "Value21");
            testMap2.Set<BigInteger, string>(new BigInteger(2), "Value22");
            testMap2.Set<BigInteger, string>(new BigInteger(3), "Value23");
            testMap2.Set<BigInteger, string>(new BigInteger(4), "Value24");

            var count = 0;
            testMap.Visit<string, string>((key, value) => {
                count++;
            });

            testMap2.Visit<BigInteger, string>((key, value) => {
                count++;

            });

            Assert.AreEqual(count, (int)testMap.Count() + testMap2.Count());
        }

        [TestMethod]
        public void TestDBStorageAddManySameKey()
        {
            int count = 20;
            int threadCount = 20;
            List<Thread> threadList = new List<Thread>();

            for (int i = 0; i < threadCount; i++)
            {
                int tmp = i;
                threadList.Add(new Thread(() => AddMany(_testStorage, count, tmp)));
            }

            foreach (Thread t in threadList)
            {
                t.Start();
            }

            foreach (Thread t in threadList)
            {
                t.Join();
            }

            Assert.AreEqual(count, (int)_testStorage.Count);
        }

        [TestMethod]
        public void TestDBStorageAddManyDifKey()
        {
            int count = 20;
            int threadCount = 15;
            List<Thread> threadList = new List<Thread>();

            for (int i = 0; i < threadCount; i++)
            {
                int tmp = i;
                threadList.Add(new Thread(() => AddMany(_testStorage, count, tmp, true)));
            }

            foreach (Thread t in threadList)
            {
                t.Start();
            }

            foreach (Thread t in threadList)
            {
                t.Join();
            }

            Assert.AreEqual(count*threadCount, (int)_testStorage.Count);
        }

        private void AddMany(KeyValueStore<string, string> kvstore, int count, int threadNum, bool addThreadNum=false)
        {
            for (int x = 0; x < count; x++)
            {
                string key = "";
                if (addThreadNum)
                {
                    key = string.Format("{0}key{1}", x, threadNum);
                }
                else
                {
                    key = string.Format("{0}key", x);
                }
                var val = string.Format("{0}val{1}", x, threadNum);
                kvstore.Set(key, val);
            }
        }
    }
}
