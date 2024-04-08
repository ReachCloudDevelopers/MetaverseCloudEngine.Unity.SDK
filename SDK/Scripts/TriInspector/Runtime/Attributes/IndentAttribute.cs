using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class IndentAttribute : Attribute
    {
        public int Indent { get; }

        public IndentAttribute() : this(1)
        {
        }

        public IndentAttribute(int indent)
        {
            Indent = indent;
        }
    }
}