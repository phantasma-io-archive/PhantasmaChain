using System;
using System.Reflection;
using System.Collections.Generic;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.VM;
using Phantasma.Storage;
using System.Text;

namespace Phantasma.Domain
{
    public abstract class SmartContract : IContract
    {
        public const int SecondsInDay = 86400;

        public abstract ExecutionContext CreateContext();

        public ContractInterface ABI { get; private set; }
        public abstract string Name { get; }

        public BigInteger Order { get; internal set; } // TODO remove this?

        private readonly Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer()); // TODO remove this?

        public IRuntime Runtime { get; protected set; }

        private Dictionary<string, MethodInfo> _methodTable = new Dictionary<string, MethodInfo>();

        private Address _address;
        public Address Address
        {
            get
            {
                if (_address.IsNull)
                {
                   _address = GetAddressForName(Name);
                }

                return _address;
            }
        }

        public SmartContract()
        {
            this.Order = 0;

            BuildMethodTable();

            _address = Address.Null;
        }

        public static Address GetAddressForNative(NativeContractKind kind)
        {
            return GetAddressForName(kind.GetName());
        }

        public static Address GetAddressForName(string name)
        {
            return Address.FromHash(name);
        }

        public static byte[] GetKeyForField(string contractName, string fieldName, bool isProtected)
        {
            Throw.If(string.IsNullOrEmpty(contractName), "invalid contract name");
            Throw.If(string.IsNullOrEmpty(fieldName), "invalid field name");

            string prefix = isProtected ? "." : "";

            return Encoding.UTF8.GetBytes($"{prefix}{contractName}.{fieldName}");
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
                var parameters = new List<ContractParameter>();
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
                        parameters.Add(new ContractParameter(srcParam.Name, vmtype));
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

        public bool HasInternalMethod(string methodName)
        {
            return _methodTable.ContainsKey(methodName);
        }

        public object CallInternalMethod(IRuntime runtime, string name, object[] args)
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

        private object CastArgument(IRuntime runtime, object arg, Type expectedType)
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
            if (expectedType == receivedType)
            {
                return arg;
            }

            if (expectedType.IsArray)
            {
                if (expectedType == typeof(byte[]))
                {
                    if (receivedType == typeof(string))
                    {
                        return Encoding.UTF8.GetBytes((string)arg);
                    }

                    if (receivedType == typeof(BigInteger))
                    {
                        return ((BigInteger)arg).ToSignedByteArray();
                    }

                    if (receivedType == typeof(Hash))
                    {
                        return ((Hash)arg).ToByteArray();
                    }

                    if (receivedType == typeof(Address))
                    {
                        return ((Address)arg).ToByteArray();
                    }

                    throw new Exception("cannot cast this object to a byte array");
                }
                else
                {
                    var dic = (Dictionary<VMObject, VMObject>)arg;
                    var elementType = expectedType.GetElementType();
                    var array = Array.CreateInstance(elementType, dic.Count);
                    for (int i = 0; i < array.Length; i++)
                    {
                        var key = new VMObject();
                        key.SetValue(i);

                        var val = dic[key].Data;
                        val = CastArgument(runtime, val, elementType);
                        array.SetValue(val, i);
                    }
                    return array;
                }
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
                    var text = (string)arg;
                    Address address;

                    if (Address.IsValidAddress(text))
                    {
                        address = Address.FromText(text);
                    }
                    else
                    {
                        // when a name string is passed instead of an address we do an automatic lookup and replace
                        address = runtime.LookUpName(text);
                    }

                    return address;
                }
            }

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
            }
            
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
    }
}
