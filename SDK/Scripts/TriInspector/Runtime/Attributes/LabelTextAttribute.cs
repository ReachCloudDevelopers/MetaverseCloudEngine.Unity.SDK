using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LabelTextAttribute : Attribute
    {
        public string Text { get; }

        public LabelTextAttribute(string text)
        {
            Text = text;
        }
    }
}