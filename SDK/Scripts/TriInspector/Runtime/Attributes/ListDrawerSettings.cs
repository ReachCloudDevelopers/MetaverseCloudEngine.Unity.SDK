using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    
    public class ListDrawerSettingsAttribute : Attribute
    {
        public bool Draggable { get; set; } = true;
        public bool HideAddButton { get; set; }
        public bool HideRemoveButton { get; set; }
        public bool AlwaysExpanded { get; set; }
    }
}