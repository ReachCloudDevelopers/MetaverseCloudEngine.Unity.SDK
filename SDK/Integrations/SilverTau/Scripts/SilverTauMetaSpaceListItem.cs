using MetaverseCloudEngine.Common.Models.DataTransfer;
using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SilverTau
{
    /// <summary>
    /// Represents a single item in the SilverTau MetaSpace list.
    /// </summary>
    public class SilverTauMetaSpaceListItem : TriInspectorMonoBehaviour
    {
        public UnityEvent<string> onName = new();
        public string dateFormat = "dd/MM/yy";
        public UnityEvent<string> onDate = new();
        
        private SilverTauMetaSpaceList _list;
        
        /// <summary>
        /// The MetaSpace data associated with this item.
        /// </summary>
        public MetaSpaceDto MetaSpace { get; private set; }

        /// <summary>
        /// Repaints the item with the provided MetaSpace data.
        /// </summary>
        /// <param name="metaSpace">The meta space data.</param>
        /// <param name="list">The list that created this item.</param>
        public void Repaint(MetaSpaceDto metaSpace, SilverTauMetaSpaceList list)
        {
            _list = list;
            MetaSpace = metaSpace;
            onName.Invoke(metaSpace.Name.Replace("ENV_SCAN:", string.Empty));
            onDate.Invoke((metaSpace.UpdatedDate ?? metaSpace.CreatedDate).ToString(dateFormat));
        }

        /// <summary>
        /// Call this method to delete the item from the list.
        /// </summary>
        public void Delete()
        {
            if (!_list) return;
            _list.OnDeleteRequested(this);
        }
    }
}