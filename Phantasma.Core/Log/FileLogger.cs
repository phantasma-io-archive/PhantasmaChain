using System.IO;

namespace Phantasma.Core.Log
{
    public class FileLog : Logger
    {
        readonly string fileName;

        public FileLog(string file)
        {
            fileName = file;
        }

        public override void Write(LogEntryKind kind, string msg)
        {
            File.AppendAllLines(fileName, new string[] { msg });
        }
    }
}
