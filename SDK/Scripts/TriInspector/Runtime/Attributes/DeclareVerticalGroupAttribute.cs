using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class DeclareVerticalGroupAttribute : DeclareGroupBaseAttribute
    {
        public DeclareVerticalGroupAttribute(string path) : base(path)
        {
        }
    }
}