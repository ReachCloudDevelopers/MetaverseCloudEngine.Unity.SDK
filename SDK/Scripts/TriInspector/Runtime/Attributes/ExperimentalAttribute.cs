using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage((AttributeTargets.Class | AttributeTargets.Struct))]
    public sealed class ExperimentalAttribute : Attribute
    {
    }
}