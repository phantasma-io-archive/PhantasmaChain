using LunarLabs.Parser;
using System;

namespace Phantasma.API
{
    public static class APIUtils
    {
        public static DataNode FromAPIResult(IAPIResult input)
        {
            DataNode result;

            if (input is SingleResult singleResult)
            {
                result = DataNode.CreateValue("");
                result.Value = singleResult.value.ToString();
                //result = DataNode.CreateObject();
                //result.AddValue(singleResult);
            }
            else
            if (input is ArrayResult arrayResult)
            {
                result = DataNode.CreateArray();
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

                            result.AddNode(entry);
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
