using System;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class HideReferencePickerAttribute : Attribute
    {
    }
}