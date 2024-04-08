using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class TitleAttribute : Attribute
    {
        public string Title { get; }

        public TitleAttribute(string title)
        {
            Title = title;
        }
    }
}