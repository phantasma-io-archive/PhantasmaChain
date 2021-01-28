using System;
using System.IO;

namespace Phantasma.Core.Log
{
    public class FileLogger : Logger
    {
        private static StreamWriter sw;

        public FileLogger(string file, LogLevel level) : base(level)
        {
            if (sw != null)
            {
                return;
            }
            sw = new StreamWriter(file, true);
        }

        ~FileLogger()
        {
            sw.Dispose();
            sw.Close();
        }

        protected override void Write(LogLevel kind, string msg)
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var output = $"{date} {kind} {msg}";

            sw.WriteLine(output);
            sw.Flush();
        }
    }
}
