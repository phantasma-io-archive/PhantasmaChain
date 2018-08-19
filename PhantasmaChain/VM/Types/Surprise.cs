namespace Phantasma.VM.Types
{

    public interface ISurprise<T> : IInteropObject
    {
        T Value { get; }
        Timestamp Timestamp { get; }
        bool Hidden { get; }
    }
}
