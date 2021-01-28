using System;

namespace Phantasma.Core.Log
{
    public class ConsoleLogger : Logger
    {
        public ConsoleLogger(LogLevel level) : base(level)
        {

        }

        protected override void Write(LogLevel kind, string msg)
        {
            var color = Console.ForegroundColor;
            switch (kind)
            {
                case LogLevel.Error: Console.ForegroundColor = ConsoleColor.Red; break;
                case LogLevel.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
                case LogLevel.Message: Console.ForegroundColor = ConsoleColor.Gray; break;
                case LogLevel.Success: Console.ForegroundColor = ConsoleColor.Green; break;
                case LogLevel.Debug: Console.ForegroundColor = ConsoleColor.Cyan; break;
                default: return;
            }
            Console.WriteLine(msg);
            Console.ForegroundColor = color;
        }
    }
}
