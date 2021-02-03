namespace Phantasma.Core.Log
{
    /// <summary>
    /// Represents the kind of the log message.
    /// </summary>
    public enum LogLevel
    {
        None = 0,
        Success = 1,
        Error = 2,
        Warning = 3,
        Message = 4,
        Debug = 5,
        Maximum = 6
    }
    // Don't change the order here, its important, must be ordered by importance
}
