using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class ShowIfAttribute : HideIfAttribute
    {
        public ShowIfAttribute(string condition) : this(condition, true)
        {
        }

        public ShowIfAttribute(string condition, object value) : base(condition, value)
        {
            Inverse = true;
        }
    }
}