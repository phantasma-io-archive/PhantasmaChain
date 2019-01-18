using LunarLabs.Parser;
using Phantasma.IO;
using System;

namespace Phantasma.API
{
    public static class APIUtils
    {
        public static DataNode FromAPIResult(IAPIResult input)
        {
            return FromObject(input, null);
        }

        private static DataNode FromObject(object input, string name)
        {
            DataNode result;

            if (input is SingleResult singleResult)
            {
                result = DataNode.CreateValue(singleResult.value);
            }
            else
            if (input is ArrayResult arrayResult)
            {
                result = DataNode.CreateArray(name);
                foreach (var item in arrayResult.values)
                {
                    DataNode itemNode;

                    if (item is IAPIResult)
                    {
                        itemNode = FromAPIResult((IAPIResult)item);
                    }
                    else
                    {
                        itemNode = DataNode.CreateObject();
                        itemNode.AddValue(item);
                    }

                    result.AddNode(itemNode);
                }
            }
            else
            {
                result = DataNode.CreateObject(name);

                var type = input.GetType();
                var fields = type.GetFields();

                foreach (var field in fields)
                {
                    if (field.FieldType.IsArray)
                    {
                        var array = (Array)field.GetValue(input);
                        if (array != null && array.Length > 0)
                        {
                            var entry = DataNode.CreateArray(field.Name);
                            foreach (var item in array)
                            {
                                DataNode itemNode;

                                if (item is IAPIResult apiResult)
                                {
                                    itemNode = FromAPIResult(apiResult);
                                }
                                else
                                {
                                    itemNode = DataNode.CreateValue(item);
                                }

                                entry.AddNode(itemNode);
                            }

                            result.AddNode(entry);
                        }
                    }
                    else
                    if (field.FieldType.IsStructOrClass())
                    {
                        var val = field.GetValue(input);
                        if (val != null)
                        {
                            var node = FromObject(val, field.Name);
                            result.AddNode(node);
                        }
                    }
                    else
                    {
                        var val = field.GetValue(input);
                        if (val != null)
                        {
                            result.AddField(field.Name, val);
                        }
                    }
                }
            }

            return result;
        }
    }
}
