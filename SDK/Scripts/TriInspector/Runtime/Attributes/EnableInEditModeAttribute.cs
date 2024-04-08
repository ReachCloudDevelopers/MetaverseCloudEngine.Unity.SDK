using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class EnableInEditModeAttribute : DisableInEditModeAttribute
    {
        public EnableInEditModeAttribute()
        {
            Inverse = true;
        }
    }
}