using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Phantasma.RpcClient.Client
{
    public static class DefaultJsonSerializerSettingsFactory
    {
        public static JsonSerializerSettings BuildDefaultJsonSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
        }
    }

    public class NullParamsFirstElementResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            return type.GetTypeInfo().DeclaredProperties
                .Select(p =>
                {
                    var jp = CreateProperty(p, memberSerialization);
                    jp.ValueProvider = new NullParamsValueProvider(p);
                    return jp;
                }).ToList();
        }
    }

    public class NullParamsValueProvider : IValueProvider
    {
        private readonly PropertyInfo _memberInfo;

        public NullParamsValueProvider(PropertyInfo memberInfo)
        {
            _memberInfo = memberInfo;
        }

        public object GetValue(object target)
        {
            var result = _memberInfo.GetValue(target);
            if (_memberInfo.Name == "RawParameters")
            {
                if (result is object[] array && array.Length == 1 && array[0] == null)
                    result = "[]";
            }
            return result;
        }

        public void SetValue(object target, object value)
        {
            _memberInfo.SetValue(target, value);
        }
    }
}
