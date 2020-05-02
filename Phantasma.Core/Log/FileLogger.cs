using System;
using System.IO;

namespace Phantasma.Core.Log
{
    public class FileLogger : Logger
    {
        private static object _lock = new object();
        private static StreamWriter sw;

        public FileLogger(string file)
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

        public override void Write(LogEntryKind kind, string msg)
        {
            lock (_lock)
            {
                sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + msg);
                sw.Flush();
            }
        }
    }
}
