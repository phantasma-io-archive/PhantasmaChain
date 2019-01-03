using LunarLabs.Parser;
using System;

namespace Phantasma.API
{
    internal static class APIUtils
    {
        internal static DataNode FromAPIResult(IAPIResult input)
        {
            DataNode result;

            if (input is SingleResult)
            {
                result = DataNode.CreateObject();
                result.AddValue(((SingleResult)input).value);
            }
            else
            if (input is ArrayResult)
            {
                result = DataNode.CreateArray();
                var array = (ArrayResult)input;
                foreach (var item in array.values)
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
                result = DataNode.CreateObject();

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

                                if (item is IAPIResult)
                                {
                                    itemNode = FromAPIResult((IAPIResult)item);
                                }
                                else
                                {
                                    itemNode = DataNode.CreateObject();
                                    itemNode.AddValue(item);
                                }

                                entry.AddNode(itemNode);
                            }
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
