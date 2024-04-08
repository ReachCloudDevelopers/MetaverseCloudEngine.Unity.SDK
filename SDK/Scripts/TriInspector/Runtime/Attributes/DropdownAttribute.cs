using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class DropdownAttribute : Attribute
    {
        public string Values { get; }

        public DropdownAttribute(string values)
        {
            Values = values;
        }
    }
}