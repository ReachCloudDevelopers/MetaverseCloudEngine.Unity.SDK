using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class DisableInEditModeAttribute : Attribute
    {
        public bool Inverse { get; protected set; }
    }
}