using System.Collections.Generic;
using Phantasma.Cryptography;

namespace Phantasma.VM
{
    public enum ExecutionState
    {
        Running,
        Break,
        Fault,
        Halt
    }

    public abstract class ExecutionContext
    {
        public abstract string Name { get; }

        private Address _address;
        public Address Address {
            get
            {
                if (_address.IsNull)
                {
                    _address = Address.FromHash(Name);
                }

                return _address;
            }
        }

        public abstract ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack);

        public override string ToString()
        {
            return Name;
        }
    }
}
