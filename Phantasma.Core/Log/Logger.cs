using System;

namespace Phantasma.Core.Log
{
    public abstract class Logger
    {
        public readonly LogLevel Level;

        private static object _lock = new object();

        public Logger(LogLevel level)
        {
            this.Level = level;
        }

        public static Logger Init(Logger log)
        {
            if (log == null)
            {
                return DummyLogger.Instance;
            }

            return log;
        }

        protected abstract void Write(LogLevel kind, string msg);

        public void RouteMessage(LogLevel kind, string msg)
        {
            if (kind > this.Level)
            {
                return;
            }

            lock (_lock)
            {
                this.Write(kind, msg);
            }
        }

        public void Message(string msg)
        {
            RouteMessage(LogLevel.Message, msg);
        }

        public void Debug(string msg)
        {
            RouteMessage(LogLevel.Debug, msg);
        }

        public void Warning(string msg)
        {
            RouteMessage(LogLevel.Warning, msg);
        }

        public void Error(string msg)
        {
            RouteMessage(LogLevel.Error, msg);
        }


        public void Success(string msg)
        {
            RouteMessage(LogLevel.Success, msg);
        }

        public void Exception(Exception ex)
        {
            Error(ex.ToString());
        }
    }
}
