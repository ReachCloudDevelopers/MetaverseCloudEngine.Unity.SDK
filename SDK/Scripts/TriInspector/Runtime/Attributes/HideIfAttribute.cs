using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class HideIfAttribute : Attribute
    {
        public HideIfAttribute(string condition) : this(condition, true)
        {
        }

        public HideIfAttribute(string condition, object value)
        {
            Condition = condition;
            Value = value;
        }

        public string Condition { get; }
        public object Value { get; }

        public bool Inverse { get; protected set; }
    }
}