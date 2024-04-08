using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class GroupNextAttribute : Attribute
    {
        public GroupNextAttribute(string path)
        {
            Path = path;
        }

        [CanBeNull] public string Path { get; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class UnGroupNextAttribute : GroupNextAttribute
    {
        public UnGroupNextAttribute() : base(null)
        {
        }
    }
}