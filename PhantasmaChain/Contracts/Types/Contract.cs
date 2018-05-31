using System;

namespace Phantasma.Contracts.Types
{
    public interface IContract
    {
        Address Address { get; }
    }

    public abstract class Contract: IContract 
    {
        public void Expect(bool assertion)
        {
            throw new NotImplementedException();
        }

        public Address Address { get; }

        public IRuntime Runtime;
    }
}
