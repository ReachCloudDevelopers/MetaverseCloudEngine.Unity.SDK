using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Attributes
{
    /// <summary>
    /// An attribute which turns a string field into a organization picker.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class OrganizationIdPropertyAttribute : PropertyAttribute
    {
    }
}