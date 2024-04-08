using System.Linq;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class PlayerGroupPicker : PickerEditor
    {
        protected override bool RequestPickables(int offset, int count, string filter)
        {
            var metaSpace = MVUtils.FindObjectsOfTypeNonPrefabPooled<MetaSpace>(true).FirstOrDefault();
            if (metaSpace != null)
            {
                var groups = metaSpace.PlayerGroupOptions.PlayerGroups;
                if (!string.IsNullOrEmpty(filter))
                    groups = groups.Where(x => !string.IsNullOrEmpty(x.displayName) && x.displayName.ToLower().Contains(filter.ToLower())).ToArray();
                groups = groups
                    .Skip(offset)
                    .Take(count)
                    .ToArray();
                OnPickablesReceived(groups);
                return true;
            }

            return false;
        }
    }
}