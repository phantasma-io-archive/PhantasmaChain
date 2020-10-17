using System;

namespace Phantasma.Core.Log
{
    public abstract class Logger
    {
        public LogEntryKind Level = LogEntryKind.Debug;

        public abstract void Write(LogEntryKind kind, string msg);

        public void Message(string msg)
        {
            if (this.Level < LogEntryKind.Message)
            {
                return;
            }

            Write(LogEntryKind.Message, msg);
        }

        public void Debug(string msg)
        {
            if (this.Level < LogEntryKind.Debug)
            {
                return;
            }

            Write(LogEntryKind.Debug, msg);
        }

        public static Logger Init(Logger log)
        {
            if (log == null)
            {
                return DummyLogger.Instance;
            }

            return log;
        }

        public void Warning(string msg)
        {
            if (this.Level < LogEntryKind.Warning)
            {
                return;
            }

            Write(LogEntryKind.Warning, msg);
        }

        public void Error(string msg)
        {
            if (this.Level < LogEntryKind.Error)
            {
                return;
            }

            Write(LogEntryKind.Error, msg);
        }


        public void Success(string msg)
        {
            if (this.Level < LogEntryKind.Success)
            {
                return;
            }

            Write(LogEntryKind.Success, msg);
        }

        public void Exception(Exception ex)
        {
            Error(ex.ToString());
        }
    }
}
