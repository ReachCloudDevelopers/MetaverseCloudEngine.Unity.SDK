#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [CustomEditor(typeof(TerrainSurface))]

    public class TerrainSurfaceEditor : Editor
    {
        TerrainData terDat;
        TerrainSurface targetScript;
        string[] surfaceNames;

        public override void OnInspectorGUI() {
            GroundSurfaceMaster surfaceMaster = FindObjectOfType<GroundSurfaceMaster>();
            if (surfaceMaster == null)
            {
                EditorGUILayout.HelpBox("Add the Ground Surface Master to your scene to get easier to use inspector.", MessageType.Info);
                if (GUILayout.Button("Add Ground Surface Master to Scene"))
                {
                    GroundSurfaceMaster inst = GroundSurfaceMaster.Instance;
                    if (inst)
                        Selection.SetActiveObjectWithContext(inst, inst);
                }
                base.OnInspectorGUI();
                return;
            }
            
            targetScript = (TerrainSurface)target;
            Undo.RecordObject(targetScript, "Terrain Surface Change");

            if (targetScript.GetComponent<Terrain>().terrainData) {
                terDat = targetScript.GetComponent<Terrain>().terrainData;
            }

            EditorGUILayout.LabelField("Textures and Surface Types:", EditorStyles.boldLabel);

            surfaceNames = new string[surfaceMaster.surfaceTypes.Length];

            for (int i = 0; i < surfaceNames.Length; i++) {
                surfaceNames[i] = surfaceMaster.surfaceTypes[i].name;
            }

            if (targetScript.surfaceTypes.Length > 0) {
                for (int j = 0; j < targetScript.surfaceTypes.Length; j++) {
                    DrawTerrainInfo(terDat, j);
                }
            }
            else {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("<No terrain textures found>");
            }

            if (GUI.changed) {
                EditorUtility.SetDirty(targetScript);
            }
        }

        void DrawTerrainInfo(TerrainData ter, int index) {
            EditorGUI.indentLevel = 1;
            targetScript.surfaceTypes[index] = EditorGUILayout.Popup(terDat.terrainLayers[index].diffuseTexture.name, targetScript.surfaceTypes[index], surfaceNames);
            EditorGUI.indentLevel++;
        }
    }
}
#endif