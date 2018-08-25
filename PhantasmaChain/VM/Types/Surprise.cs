namespace Phantasma.VM.Types
{
    public interface ISurprise<T>
    {
        T Value { get; }
        Timestamp Timestamp { get; }
        bool Hidden { get; }
    }
}
