using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class DeclareBoxGroupAttribute : DeclareGroupBaseAttribute
    {
        public DeclareBoxGroupAttribute(string path) : base(path)
        {
            Title = path;
        }

        public string Title { get; set; }
        public bool HideTitle { get; set; }
    }
}