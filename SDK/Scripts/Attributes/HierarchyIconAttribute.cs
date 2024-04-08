using System;

namespace MetaverseCloudEngine.Unity.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HierarchyIconAttribute : Attribute
    {
        public HierarchyIconAttribute(string iconName)
        {
            IconName = iconName;
        }
        
        public string IconName { get; }
    }
}