using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ShowInInspectorAttribute : Attribute
    {
    }
}