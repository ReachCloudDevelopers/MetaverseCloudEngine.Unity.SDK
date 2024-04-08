using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class DisallowNullAttribute : PropertyAttribute
    {
    }
}