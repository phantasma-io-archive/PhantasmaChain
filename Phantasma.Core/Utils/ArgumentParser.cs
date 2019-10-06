using System;
using System.Collections.Generic;

namespace Phantasma.Core.Utils
{
    public class Arguments
    {
        private Dictionary<string, string> entries = new Dictionary<string, string>();
        private string defaultArgument;

        public Arguments(string[] args, string prefix= "-")
        {
            var lastIndex = args.Length - 1;
            for (int index =0; index<= lastIndex; index++)
            {
                var arg = args[index];

                if (!arg.StartsWith(prefix))
                {
                    if (index == lastIndex)
                    {
                        defaultArgument = arg;
                        return;
                    }
                    else
                    {
                        throw new Exception("Invalid argument format: " + arg);
                    }
                }

                var temp = arg.Substring(prefix.Length).Split(new char[] { '=' }, 2);
                var key = temp[0];
                var val = temp.Length > 1 ? temp[1] : null;

                entries[key] = val;
            }
        }

        public string GetDefaultValue()
        {
            if (defaultArgument != null)
            {
                return defaultArgument;
            }

            throw new Exception("Not default argument found");
        }

        public string GetString(string key, string defaultVal = null)
        {
            if (entries.ContainsKey(key))
            {
                return entries[key];
            }

            if (defaultVal != null)
            {
                return defaultVal;
            }

            throw new Exception("Missing non-optional argument: " + key);
        }

        public int GetInt(string key, int defaultVal = 0)
        {
            var temp = GetString(key, defaultVal.ToString());
            int result;
            if (int.TryParse(temp, out result)) { return result; }
            return defaultVal;
        }

        public uint GetUInt(string key, uint defaultVal = 0)
        {
            var temp = GetString(key, defaultVal.ToString());
            uint result;
            if (uint.TryParse(temp, out result)) { return result; }
            return defaultVal;
        }

        public bool GetBool(string key, bool defaultVal = false)
        {
            var temp = GetString(key, defaultVal.ToString());
            bool result;
            if (bool.TryParse(temp, out result)) { return result; }
            return defaultVal;
        }

        public bool HasValue(string key)
        {
            return entries.ContainsKey(key);
        }		
    }
}
