using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.Attributes
{
    /// <summary>
    /// An attribute which turns a string field into a meta space picker.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class MetaSpaceIdPropertyAttribute : PropertyAttribute
    {
    }
}