using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonAttribute : Attribute
    {
        public ButtonAttribute()
        {
        }

        public ButtonAttribute(string name)
        {
            Name = name;
        }

        public ButtonAttribute(ButtonSizes buttonSize)
        {
            ButtonSize = (int) buttonSize;
        }

        public string Name { get; set; }
        public int ButtonSize { get; }
    }
}