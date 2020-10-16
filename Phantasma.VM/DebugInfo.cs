using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using Phantasma.Storage.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.VM
{
    public struct DebugRange
    {
        public readonly uint SourceLine;
        public readonly int StartOffset;
        public readonly int EndOffset;

        public DebugRange(uint sourceLine,  int startOffset, int endOffset)
        {
            SourceLine = sourceLine;
            StartOffset = startOffset;
            EndOffset = endOffset;
        }

        public override string ToString()
        {
            return $"Line {SourceLine} => {StartOffset} : {EndOffset}";
        }
    }

    public class DebugInfo
    {
        public readonly string FileName;
        public readonly DebugRange[] Ranges;

        public DebugInfo(string fileName, IEnumerable<DebugRange> ranges)
        {
            FileName = fileName;
            Ranges = ranges.ToArray();
        }

        // TODO optimize this with a binary search
        public int FindLine(int offset)
        {
            foreach (var range in Ranges)
            {
                if (offset >= range.StartOffset && offset <= range.EndOffset)
                {
                    return (int)range.SourceLine;
                }
            }

            return -1;
        }

        // TODO optimize this with a binary search
        public int FindOffset(int line)
        {
            foreach (var range in Ranges)
            {
                if (range.SourceLine == line)
                {
                    return (int)range.StartOffset;
                }
            }

            return -1;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteVarString(FileName);
            writer.Write((int)Ranges.Length);
            foreach (var range in Ranges)
            {
                writer.Write(range.SourceLine);
                writer.Write(range.StartOffset);
                writer.Write(range.EndOffset);
            }
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialize(writer);
                }
                return stream.ToArray();
            }
        }

        public string ToJSON()
        {
            var node = DataNode.CreateObject("debug");

            node.AddField("file_name", FileName);

            var array = DataNode.CreateArray("ranges");
            node.AddNode(array);

            foreach (var range in Ranges)
            {
                var temp = DataNode.CreateObject();
                temp.AddField("source_line", range.SourceLine);
                temp.AddField("start_ofs", range.StartOffset);
                temp.AddField("end_ofs", range.EndOffset);
                array.AddNode(temp);
            }

            return JSONWriter.WriteToString(node);
        }
    }
}
