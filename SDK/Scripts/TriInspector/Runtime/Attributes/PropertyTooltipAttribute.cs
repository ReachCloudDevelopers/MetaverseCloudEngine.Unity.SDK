using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PropertyTooltipAttribute : Attribute
    {
        public string Tooltip { get; }

        public PropertyTooltipAttribute(string tooltip)
        {
            Tooltip = tooltip;
        }
    }
}