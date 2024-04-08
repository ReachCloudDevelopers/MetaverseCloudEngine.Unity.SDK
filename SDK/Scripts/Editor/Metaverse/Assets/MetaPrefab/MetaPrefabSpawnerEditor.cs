using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using MetaverseCloudEngine.Unity.Components;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MetaPrefabSpawner))]
    public class MetaPrefabSpawnerEditor : TriInspectorMVCE.TriEditor
    {
        public override void OnInspectorGUI()
        {
            if (!MetaverseProgram.Initialized)
            {
                MetaverseEditorUtils.Info("Please wait...");
                return;
            }

            base.OnInspectorGUI();

            foreach (var t in targets)
            {
                var spawner = (MetaPrefabSpawner)t;
                if (spawner && !spawner.gameObject.IsPrefab())
                {
                    var preview = spawner.GetComponentInChildren<MetaPrefabEditorPreview>(true);
                    if (preview == null)
                    {
                        var previewObj = new GameObject("Meta Prefab Preview")
                        {
                            hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable,
                        };
                        previewObj.transform.SetParent(spawner.transform);
                        previewObj.AddComponent<MetaPrefabEditorPreview>();
                    }
                }
            }
        }
    }
}