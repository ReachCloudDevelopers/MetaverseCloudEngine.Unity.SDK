using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage((AttributeTargets.Field | AttributeTargets.Property))]
    public sealed class SceneObjectsOnlyAttribute : Attribute
    {
    }
}