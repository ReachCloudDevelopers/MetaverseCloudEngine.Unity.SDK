using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class GroupAttribute : Attribute
    {
        public GroupAttribute(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}