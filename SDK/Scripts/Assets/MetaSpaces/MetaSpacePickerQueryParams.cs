using System;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.Attributes;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    [Serializable]
    public class MetaSpacePickerQueryParams : AssetPickerQueryParams
    {
        [Header("MetaSpace")] 
        public bool queryOrganization = true;
        [OrganizationIdProperty] public string organization;
        public bool canChangeTags;
        public bool queryTags;
        public MetaSpaceTags tags;
        public MetaSpaceTags notTags;
    }
}