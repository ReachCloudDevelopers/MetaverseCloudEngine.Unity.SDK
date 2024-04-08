using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
    public class DrawWithTriInspectorAttribute : Attribute
    {
    }
}