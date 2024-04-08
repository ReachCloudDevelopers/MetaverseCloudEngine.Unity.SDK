using System;
using System.Linq;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Attributes;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(PlayerGroupIdAttribute))]
    public class PlayerGroupRecordIdPropertyDrawer : RecordIdPropertyDrawer<PlayerGroup, string, PlayerGroupPicker>
    {
        protected override string ParseRecordId(string str) => str;
        protected override string GetRecordId(PlayerGroup record) => record.identifier;
        protected override string GetRecordIdStringValue(string id) => id;
        protected override GUIContent GetRecordLabel(PlayerGroup record) => new (record.displayName, EditorGUIUtility.IconContent("SoftlockInline").image, record.identifier);
        protected override void RequestRecord(string id, Action<PlayerGroup> onSuccess, Action onFailed = null)
        {
            var metaSpace = MVUtils.FindObjectsOfTypeNonPrefabPooled<MetaSpace>(true).FirstOrDefault();
            if (metaSpace != null)
            {
                var group = metaSpace.PlayerGroupOptions.PlayerGroups.FirstOrDefault(x => x.identifier == id);
                onSuccess?.Invoke(group);
                return;
            }
            
            onFailed?.Invoke();
        }
    }
}