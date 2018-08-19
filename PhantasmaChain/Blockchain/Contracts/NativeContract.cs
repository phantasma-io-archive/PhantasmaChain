using System.Text;
using System.Reflection;
using Phantasma.Utils;
using Phantasma.VM.Types;
using Phantasma.VM.Contracts;
using System.Collections.Generic;
using Phantasma.VM;

namespace Phantasma.Blockchain.Contracts
{
    public abstract class NativeContract : Contract
    {
        private Address _address;
        public override Address Address => _address;

        public override byte[] Script => null;

        private ContractInterface _ABI;
        public override ContractInterface ABI => _ABI;

        internal abstract NativeContractKind Kind { get; }

        private Dictionary<string, MethodInfo> _methodTable = new Dictionary<string, MethodInfo>();

        public NativeContract() : base()
        {
            var bytes = Encoding.ASCII.GetBytes(this.GetType().Name);
            var hash = CryptoUtils.Sha256(bytes);
            _address = new Address(hash);

            var type = this.GetType();
            var srcMethods = type.GetMethods(BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance);
            var methods = new List<ContractMethod>();

            var ignore = new HashSet<string>(new string[] { "ToString", "GetType", "GetHashCode" });

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
                    var method = new ContractMethod(methodName, returnType, parameters);
                    methods.Add(method);
                }
            }

            _ABI = new ContractInterface(methods);
        }

        public object CallMethod(string name, object[] args)
        {
            var method = _methodTable[name];
            return method.Invoke(this, args);
        }
    }
}
