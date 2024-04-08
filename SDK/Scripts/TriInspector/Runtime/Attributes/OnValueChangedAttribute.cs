using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class OnValueChangedAttribute : Attribute
    {
        public OnValueChangedAttribute(string method)
        {
            Method = method;
        }

        public string Method { get; }
    }
}