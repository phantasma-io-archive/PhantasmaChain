namespace Phantasma.Contracts.Types
{
    public interface IContract
    {
        Address GetAddress();
    }

    public abstract class Contract: IContract 
    {
        public extern void Expect(bool assertion);

        public extern Address Address { get; }

        public extern Address GetAddress();
    }
}
