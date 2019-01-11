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
                    var args = new object[] { baseKey, (StorageContext)VM.ChangeSet};
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

        internal bool HasInternalMethod(string methodName)
        {
            return _methodTable.ContainsKey(methodName);
        }

        internal object CallInternalMethod(string name, object[] args)
        {
            Throw.If(!_methodTable.ContainsKey(name), "unknowm internal method");

            var method = _methodTable[name];
            Throw.IfNull(method, nameof(method));

            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType.IsEnum)
                {
                    var receivedType = args[i].GetType();
                    if (!receivedType.IsEnum)
                    {
                        var val = Enum.Parse(p.ParameterType, args[i].ToString());
                        args[i] = val;
                    }
                }
            }

            return method.Invoke(this, args);
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

        public bool IsParentChain(Address address)
        {
            if (Runtime.Chain.ParentChain == null)
            {
                return false;
            }
            return address == this.Runtime.Chain.ParentChain.Address;
        }

        public bool IsChildChain(Address address)
        {
            var chain = Runtime.Nexus.FindChainByAddress(address);
            if (chain == null)
            {
                return false;
            }

            return chain.ParentChain == this.Runtime.Chain;
        }
        #endregion
    }
}
