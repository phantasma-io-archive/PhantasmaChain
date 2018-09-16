namespace Phantasma.Core.Types
{
    public interface IPromise<T>
    {
        T Value { get; }
        Timestamp Timestamp { get; }
        bool Hidden { get; }
    }
}
