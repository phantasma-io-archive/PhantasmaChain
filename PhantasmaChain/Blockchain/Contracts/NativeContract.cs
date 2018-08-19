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
        public override Address Address
        {
            get
            {
                var bytes = Encoding.ASCII.GetBytes(this.GetType().Name);
                var hash = CryptoUtils.Sha256(bytes);
                return new Address(hash);
            }
        }

        public override byte[] Script => null;

        private ContractInterface _ABI;
        public override ContractInterface ABI {
            get
            {
                if (_ABI != null)
                {
                    var type = this.GetType();
                    var srcMethods = type.GetMethods(BindingFlags.Public);
                    var methods = new List<ContractMethod>();

                    foreach (var srcMethod in srcMethods)
                    {
                        var parameters = new List<VM.VMType>();
                        var srcParams = srcMethod.GetParameters();
                        bool isValid = true;

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
                            var method = new ContractMethod(srcMethod.Name, parameters);
                        }
                    }

                    _ABI = new ContractInterface(methods);
                }

                return _ABI;
            }
        }

        public abstract NativeContractKind Kind { get; }

        public NativeContract(Chain chain) : base(chain)
        {
        }
    }
}
