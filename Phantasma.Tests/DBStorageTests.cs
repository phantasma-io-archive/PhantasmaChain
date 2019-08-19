using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.RocksDB;
using Phantasma.Storage;
using System.Threading;

namespace Phantasma.Tests
{

    [TestClass]
    public class DBStorageTests
    {
        private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;
        private readonly string path = "./Storage/";

        [TestInitialize()]
        public void Initialize() 
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            _adapterFactory = (name) => new DBPartition(path + "test");
        }

        [TestCleanup()]
        public void Cleanup() 
        {
            KeyValueStore<string, string> _testStorage = new KeyValueStore<string, string>(CreateKeyStoreAdapterTest("test"));
            _testStorage.Visit((key, _) =>
            {
                _testStorage.Remove(key);
            });

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
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
            KeyValueStore<string, string> _testStorage = new KeyValueStore<string, string>(CreateKeyStoreAdapterTest("test"));
            _testStorage.Set("test1", "Value11");
            string val = _testStorage.Get("test1");
            Assert.IsTrue(val == "Value11");
        }

        [TestMethod]
        public void TestDBStorageRemove()
        {
            KeyValueStore<string, string> _testStorage = new KeyValueStore<string, string>(CreateKeyStoreAdapterTest("test"));
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
            KeyValueStore<string, string> _testStorage = new KeyValueStore<string, string>(CreateKeyStoreAdapterTest("test"));
            _testStorage.Set("test1", "Value11");
            bool result = _testStorage.ContainsKey("test1");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TestDBStorageVisit()
        {
            KeyValueStore<string, string> _testStorage = new KeyValueStore<string, string>(CreateKeyStoreAdapterTest("test"));

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
        public void TestDBStorageAddManySameKey()
        {
            int count = 20;
            int threadCount = 20;
            KeyValueStore<string, string> _testStorage = new KeyValueStore<string, string>(CreateKeyStoreAdapterTest("test"));
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
            KeyValueStore<string, string> _testStorage = new KeyValueStore<string, string>(CreateKeyStoreAdapterTest("test"));
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
