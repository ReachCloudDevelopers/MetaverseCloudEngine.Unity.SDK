using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class DeclareTabGroupAttribute : DeclareGroupBaseAttribute
    {
        public DeclareTabGroupAttribute(string path) : base(path)
        {
        }
    }
}