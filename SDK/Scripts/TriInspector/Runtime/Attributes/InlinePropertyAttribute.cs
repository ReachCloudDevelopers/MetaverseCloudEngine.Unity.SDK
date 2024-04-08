using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property |
                    AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class InlinePropertyAttribute : Attribute
    {
        public float LabelWidth { get; set; }
    }
}