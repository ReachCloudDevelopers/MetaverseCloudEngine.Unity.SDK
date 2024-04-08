using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class ShowInPlayModeAttribute : HideInPlayModeAttribute
    {
        public ShowInPlayModeAttribute()
        {
            Inverse = true;
        }
    }
}