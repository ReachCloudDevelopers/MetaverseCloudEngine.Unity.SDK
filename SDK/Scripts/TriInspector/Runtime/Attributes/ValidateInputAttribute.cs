using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ValidateInputAttribute : Attribute
    {
        public string Method { get; }

        public ValidateInputAttribute(string method)
        {
            Method = method;
        }
    }
}