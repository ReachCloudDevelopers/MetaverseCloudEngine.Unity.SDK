using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class ShowInEditModeAttribute : HideInEditModeAttribute
    {
        public ShowInEditModeAttribute()
        {
            Inverse = true;
        }
    }
}