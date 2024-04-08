using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class EnableInPlayModeAttribute : DisableInPlayModeAttribute
    {
        public EnableInPlayModeAttribute()
        {
            Inverse = true;
        }
    }
}