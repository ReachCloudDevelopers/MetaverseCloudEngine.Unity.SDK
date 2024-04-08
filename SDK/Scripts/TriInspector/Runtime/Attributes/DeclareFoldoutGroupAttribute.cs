using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class DeclareFoldoutGroupAttribute : DeclareGroupBaseAttribute
    {
        public DeclareFoldoutGroupAttribute(string path) : base(path)
        {
            Title = path;
        }

        public string Title { get; set; }
        public bool Expanded { get; set; }
    }
}