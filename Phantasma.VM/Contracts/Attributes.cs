using System;

namespace Phantasma.VM.Contracts
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class PureAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class SecretAttribute : Attribute
    {
    }
}