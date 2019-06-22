using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.VM;
using Phantasma.VM.Contracts;
using Phantasma.Storage.Context;
using Phantasma.Storage;

namespace Phantasma.Blockchain.Contracts
{
    public abstract class SmartContract : IContract
    {
        public ContractInterface ABI { get; private set; }
        public abstract string Name { get; }

        public BigInteger Order { get; internal set; } // TODO remove this?

        private readonly Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer()); // TODO remove this?

        public RuntimeVM Runtime { get; private set; }
        public StorageContext Storage => Runtime.ChangeSet;

        private Dictionary<string, MethodInfo> _methodTable = new Dictionary<string, MethodInfo>();

        public SmartContract()
        {
            this.Order = 0;

            BuildMethodTable();
        }

        internal void SetRuntimeData(RuntimeVM VM)
        {
            this.Runtime = VM;

            var contractType = this.GetType();
            FieldInfo[] fields = contractType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            var storageFields = fields.Where(x => typeof(IStorageCollection).IsAssignableFrom(x.FieldType)).ToList();

            if (storageFields.Count > 0)
            {
                foreach (var field in storageFields)
                {
                    var baseKey = $"_{this.Name}.{field.Name}".AsByteArray();
                    var args = new object[] { baseKey, (StorageContext)VM.ChangeSet };
                    var obj = Activator.CreateInstance(field.FieldType, args);

                    field.SetValue(this, obj);
                }
            }
        }

        public bool IsWitness(Address address)
        {
            if (address == this.Runtime.Chain.Address) // TODO this is not right...
            {
                return true;
            }

            if (Runtime.Transaction == null)
            {
                return false;
            }

            return Runtime.Transaction.IsSignedBy(address);
        }

        public bool IsValidator(Address address)
        {
            return Runtime.Nexus.IsValidator(address);
        }

        #region METHOD TABLE
        private void BuildMethodTable()
        {
            var type = this.GetType();

            var srcMethods = type.GetMethods(BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance);
            var methods = new List<ContractMethod>();

            var ignore = new HashSet<string>(new string[] { "ToString", "GetType", "Equals", "GetHashCode", "CallMethod", "SetTransaction" });

            foreach (var srcMethod in srcMethods)
            {
                var parameters = new List<VM.VMType>();
                var srcParams = srcMethod.GetParameters();

                var methodName = srcMethod.Name;
                if (methodName.StartsWith("get_"))
                {
                    methodName = methodName.Substring(4);
                }

                if (ignore.Contains(methodName))
                {
                    continue;
                }

                var isVoid = srcMethod.ReturnType == typeof(void);
                var returnType = isVoid ? VMType.None : VMObject.GetVMType(srcMethod.ReturnType);

                bool isValid = isVoid || returnType != VMType.None;
                if (!isValid)
                {
                    continue;
                }

                foreach (var srcParam in srcParams)
                {
                    var paramType = srcParam.ParameterType;
                    var vmtype = VMObject.GetVMType(paramType);

                    if (vmtype != VMType.None)
                    {
                        parameters.Add(vmtype);
                    }
                    else
                    {
                        isValid = false;
                        break;
                    }
                }

                if (isValid)
                {
                    _methodTable[methodName] = srcMethod;
                    var method = new ContractMethod(methodName, returnType, parameters.ToArray());
                    methods.Add(method);
                }
            }

            this.ABI = new ContractInterface(methods);
        }

        internal bool HasInternalMethod(string methodName, out BigInteger gasCost)
        {
            gasCost = 10; // TODO make this depend on method
            return _methodTable.ContainsKey(methodName);
        }

        internal object CallInternalMethod(RuntimeVM runtime, string name, object[] args)
        {
            Throw.If(!_methodTable.ContainsKey(name), "unknowm internal method");

            var method = _methodTable[name];
            Throw.IfNull(method, nameof(method));

            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = CastArgument(runtime, args[i], parameters[i].ParameterType);
            }

            return method.Invoke(this, args);
        }

        private object CastArgument(RuntimeVM runtime, object arg, Type expectedType)
        {
            if (arg == null)
            {
                if (expectedType.IsArray)
                {
                    var elementType = expectedType.GetElementType();
                    var result = Array.CreateInstance(elementType, 0);
                    return result;
                }
                throw new Exception("Invalid cast for null VM object");
            }

            var receivedType = arg.GetType();

            if (expectedType.IsArray && expectedType != typeof(byte[]))
            {
                var dic = (Dictionary<VMObject, VMObject>)arg;
                var elementType = expectedType.GetElementType();
                var array = Array.CreateInstance(elementType, dic.Count);
                for (int i=0; i<array.Length; i++)
                {
                    var key = new VMObject();
                    key.SetValue(i);

                    var val = dic[key].Data;
                    val = CastArgument(runtime, val, elementType);
                    array.SetValue(val, i);
                }
                return array;
            }
            
            if (expectedType.IsEnum)
            {
                if (!receivedType.IsEnum)
                {
                    arg = Enum.Parse(expectedType, arg.ToString());
                    return arg;
                }
            }

            if (expectedType == typeof(Address))
            {
                if (receivedType == typeof(string))
                {
                    // when a string is passed instead of an address we do an automatic lookup and replace
                    var name = (string)arg;
                    var address = runtime.Nexus.LookUpName(name);
                    return address;
                }
            }

            /*
            if (expectedType == typeof(BigInteger))
            {
                if (receivedType == typeof(string))
                {
                    var value = (string)arg;
                    if (BigInteger.TryParse(value, out BigInteger number))
                    {
                        arg = number;
                    }
                }
            }*/
            
            if (typeof(ISerializable).IsAssignableFrom(expectedType))
            {
                if (receivedType == typeof(byte[]))
                {
                    var bytes = (byte[])arg;
                    arg = Serialization.Unserialize(bytes, expectedType);
                    return arg;
                }
            }

            return arg;
        }

        #endregion

        #region SIDE CHAINS
        public bool IsChain(Address address)
        {
            return Runtime.Nexus.FindChainByAddress(address) != null;
        }

        public bool IsRootChain(Address address)
        {
            var chain = Runtime.Nexus.FindChainByAddress(address);
            if (chain == null)
            {
                return false;
            }

            return chain.IsRoot;
        }

        public bool IsSideChain(Address address)
        {
            var chain = Runtime.Nexus.FindChainByAddress(address);
            if (chain == null)
            {
                return false;
            }

            return !chain.IsRoot;
        }

        public bool IsAddressOfParentChain(Address address)
        {
            if (Runtime.Chain.IsRoot)
            {
                return false;
            }

            return address == this.Runtime.ParentChain.Address;
        }

        public bool IsAddressOfChildChain(Address address)
        {
            var parentName = Runtime.Nexus.GetParentChainByAddress(address);
            var parent = Runtime.Nexus.FindChainByName(parentName);
            if (parent== null)
            {
                return false;
            }

            return parent.Address == this.Runtime.Chain.Address;
        }
        #endregion
    }
}
