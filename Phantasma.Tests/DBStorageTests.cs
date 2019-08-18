using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.RocksDB;
using Phantasma.Storage;

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
    }
}
