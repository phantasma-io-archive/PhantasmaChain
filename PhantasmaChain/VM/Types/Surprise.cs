namespace Phantasma.VM.Types
{

    [VMType]
    public interface ISurprise<T>
    {
        T Value { get; }
        Timestamp Timestamp { get; }
        bool Hidden { get; }
    }
}
