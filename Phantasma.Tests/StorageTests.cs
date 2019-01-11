using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.VM.Contracts;
using Phantasma.Core.Utils;
using Phantasma.Blockchain.Storage;
using Phantasma.VM;
using Phantasma.VM.Utils;

namespace Phantasma.Tests
{
    [TestClass]
    public class StorageTests
    {
        [TestMethod]
        public void TestStorageList()
        {
            var context = new MemoryStorageContext();
            var list = new StorageList("test".AsByteArray(), context);
            Assert.IsTrue(list.Count() == 0);

            list.Add("hello");
            list.Add("world");
            Assert.IsTrue(list.Count() == 2);

            list.RemoveAt<string>(0);
            Assert.IsTrue(list.Count() == 1);

            var temp = list.Get<string>(0);
            Assert.IsTrue(temp == "world");

            list.Replace<string>(0, "hello");

            temp = list.Get<string>(0);
            Assert.IsTrue(temp == "hello");
        }

        [TestMethod]
        public void TestStorageMap()
        {
            var context = new MemoryStorageContext();

            var map = new StorageMap("test".AsByteArray(), context);
            Assert.IsTrue(map.Count() == 0);

            map.Set(1, "hello");
            map.Set(3, "world");
            Assert.IsTrue(map.Count() == 2);

            Assert.IsFalse(map.ContainsKey(0));
            Assert.IsTrue(map.ContainsKey(1));
            Assert.IsFalse(map.ContainsKey(2));
            Assert.IsTrue(map.ContainsKey(3));

            map.Remove(2);
            Assert.IsTrue(map.Count() == 2);

            map.Remove(1);
            Assert.IsTrue(map.Count() == 1);
        }

        [TestMethod]
        public void TestStorageMapList()
        {
            var context = new MemoryStorageContext();

            var map = new StorageMap("map".AsByteArray(), context);
            Assert.IsTrue(map.Count() == 0);

            var list = new StorageList("list".AsByteArray(), context);
            Assert.IsTrue(list.Count() == 0);

            list.Add("hello");
            list.Add("world");

            int key = 123;
            map.Set(key, list);
            var count = map.Count();
            Assert.IsTrue(count == 1);

            int otherKey = 21;
            var other = map.Get<int, StorageList>(otherKey);
            Assert.IsTrue(other.Count() == 0);

            var another = map.Get<int, StorageList>(key);
            count = another.Count();
            Assert.IsTrue(count == 2);

            // note: here we remove from one list and count the other, should be same since both are references to same storage list
            another.RemoveAt<string>(0);
            count = list.Count();
            Assert.IsTrue(count == 1);
        }
    }
}
