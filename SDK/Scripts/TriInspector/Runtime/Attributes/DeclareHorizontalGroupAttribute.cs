using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class DeclareHorizontalGroupAttribute : DeclareGroupBaseAttribute
    {
        public DeclareHorizontalGroupAttribute(string path) : base(path)
        {
        }
    }
}