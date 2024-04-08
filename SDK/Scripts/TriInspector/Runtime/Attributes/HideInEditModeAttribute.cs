using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class HideInEditModeAttribute : Attribute
    {
        public bool Inverse { get; protected set; }
    }
}