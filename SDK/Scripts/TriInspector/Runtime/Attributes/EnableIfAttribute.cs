using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class EnableIfAttribute : DisableIfAttribute
    {
        public EnableIfAttribute(string condition) : this(condition, true)
        {
        }

        public EnableIfAttribute(string condition, object value) : base(condition, value)
        {
            Inverse = true;
        }
    }
}