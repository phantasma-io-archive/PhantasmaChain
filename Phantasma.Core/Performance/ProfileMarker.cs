using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Phantasma.Core.Performance
{
    public class ProfileSession : IDisposable
    {
        [ThreadStatic]
        public static ProfileSession CurrentSession;

        public ProfileSession(System.IO.Stream o)
        {
            output = o;
            CurrentSession = this;
        }
        void IDisposable.Dispose()
        {
            Stop();
        }

        public void Stop()
        {
            if (CurrentSession == this)
                CurrentSession = null;

            if (output == null)
                return;

            writer = new System.IO.StreamWriter(output);

            bool first = true;

            //convert to microseconds
            double scale = 1000000.0 / System.Diagnostics.Stopwatch.Frequency;

            BeginDocument();
            var stack = new List<int>();
            for (int i = 0, end = events.Count; i != end; ++i)
            {
                for (int j = stack.Count; j-- > 0;)
                {
                    var p = events[stack[j]];
                    if (p.parentIdx == i)
                        break;
                    EndEvent(p.name, p.end * scale);
                }
                var e = events[i];
                BeginEvent(e.name, e.start * scale, first);
                first = false;

                if (i + 1 < end && events[i + 1].parentIdx == i)
                {
                    stack.Add(i);
                }
                else
                {
                    EndEvent(e.name, e.end * scale);
                }
            }
            for (int j = stack.Count; j-- > 0;)
            {
                var p = events[stack[j]];
                EndEvent(p.name, p.end * scale);
            }
            EndDocument();

            writer.Flush();
            writer.Dispose();
            output.Close();
            output.Dispose();
            output = null;
        }

        private void BeginDocument()
        {
            writer.Write("{\"traceEvents\":[");
        }
        private void BeginEvent(string name, double time, bool first)
        {
            if (first)
                writer.Write("{");
            else
                writer.Write(",{");
            writer.Write(String.Format("\"name\":\"{0}\",", name));
            writer.Write(String.Format("\"ts\":{0},", (Int64)time));
            writer.Write("\"ph\":\"B\",");
            writer.Write("\"args\":{},");
            writer.Write("\"pid\":0,\"tid\":0");
            writer.Write("}");
        }
        private void EndEvent(string name, double time)
        {
            writer.Write(",{");
            writer.Write(String.Format("\"name\":\"{0}\",", name));
            writer.Write(String.Format("\"ts\":{0},", (Int64)time));
            writer.Write("\"ph\":\"E\",");
            writer.Write("\"args\":{},");
            writer.Write("\"pid\":0,\"tid\":0");
            writer.Write("}");
        }
        private void EndDocument()
        {
            writer.Write("], \"otherData\":{} }");
        }

        public void Push(string name)
        {
            long time = System.Diagnostics.Stopwatch.GetTimestamp();
            int newIdx = events.Count;
            events.Add(new Event { name=name, start = time, end=0, parentIdx=top });
            top = newIdx;
        }
        public void Pop(string name)
        {
            long time = System.Diagnostics.Stopwatch.GetTimestamp();
            Event e = events[top];
            e.end = time;
            events[top] = e;
            top = e.parentIdx;
        }

        private struct Event
        {
            public string name;
            public long start;
            public long end;
            public int parentIdx;
        }
        private List<Event> events = new List<Event>();
        private int top = -1;
        private System.IO.Stream output;
        private System.IO.TextWriter writer;
    }

    public struct ProfileMarker : IDisposable
    {
        private readonly string Name;
        private readonly ProfileSession Session;
        public ProfileMarker(string name)
        {
            Session = ProfileSession.CurrentSession;
            if(Session != null)
            {
                Name = name;
                Session.Push(name);
            }
            else
                Name = null;
        }
        public ProfileMarker(string name, ProfileMarker parent)
        {
            Session = parent.Session;
            if (Session != null)
            {
                Name = name;
                Session.Push(name);
            }
            else
                Name = null;
        }
        void IDisposable.Dispose()
        {
            if (Session != null)
            {
                Session.Pop(Name);
            }
        }
    }
}