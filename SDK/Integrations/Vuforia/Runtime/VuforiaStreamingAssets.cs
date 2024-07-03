using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TriInspectorMVCE;
using UnityEngine;
using Vuforia;
using Object = System.Object;

namespace MetaverseCloudEngine.Unity.Vuforia
{
#if METAVERSE_CLOUD_ENGINE_INTERNAL
    [CreateAssetMenu(menuName = "Internal/" + nameof(VuforiaStreamingAssets))]
#endif
    public class VuforiaStreamingAssets : TriInspectorScriptableObject
    {
        private const string XmlMagik = "<?xml";
        private const string DatMagik = "PK\u0003\u0004\u0014";
        private const string ThreeDTMagik = "PK\u0003\u0004\u0014";

        [ReadOnly]
        [ShowInInspector]
        public float TotalSizeMB
        {
            get
            {
                var totalSize = vuforiaFiles.Sum(file => file.Size);
                return (float)(totalSize / 1024.0 / 1024.0);
            }
        }
        
        public static string VuforiaPath => Path.Combine(Application.temporaryCachePath, "VuforiaStreamingAssets");
        private static string VuforiaEditorDatabaseAssetsPath => Path.Combine(Application.streamingAssetsPath, "Vuforia");
        private static string VuforiaEditorOcclusionAssetsPath => Path.Combine("Assets", "Editor", "Vuforia");
        
        [Serializable]
        public class VuforiaFile
        {
            [ReadOnly]
            public string name;
            [HideInInspector]
            public byte[] data;
            
            [ReadOnly]
            [ShowInInspector]
            [LabelText("Size (bytes)")]
            public long Size => data?.Length ?? 0;
        }
        
        public VuforiaFile[] vuforiaFiles = Array.Empty<VuforiaFile>();
        
        private static VuforiaStreamingAssets _instance;
        public static VuforiaStreamingAssets Instance
        {
            get
            {
                if (_instance)
                    return _instance;
                
                _instance = Resources.Load<VuforiaStreamingAssets>(nameof(VuforiaStreamingAssets));
                
                if (_instance) 
                    return _instance;
#if UNITY_EDITOR
                _instance = CreateInstance<VuforiaStreamingAssets>();
                _instance.name = nameof(VuforiaStreamingAssets);
                UnityEditor.AssetDatabase.CreateAsset(_instance, $"Assets/Resources/{nameof(VuforiaStreamingAssets)}.asset");
                UnityEditor.AssetDatabase.SaveAssets();
#endif
                return _instance;
            }
        }

#if UNITY_EDITOR
        public static void Collect(UnityEngine.Object sourceAsset)
        {
            if (!Instance)
                return;
            
            Instance.CollectInternal(sourceAsset);
        }
#endif

#if METAVERSE_CLOUD_ENGINE_INTERNAL
        [Button("Dump Files")]
        public void Dump()
        {
            if (vuforiaFiles == null || vuforiaFiles.Length == 0)
                return;
            
            for (var i = 0; i < vuforiaFiles.Length; i++)
            {
                var file = vuforiaFiles[i];
                if (file.name.EndsWith(".xml"))
                {
                    if (!System.Text.Encoding.UTF8.GetString(file.data).StartsWith(XmlMagik))
                        continue;
                }
                else if (file.name.EndsWith(".dat"))
                {
                    if (!System.Text.Encoding.UTF8.GetString(file.data).StartsWith(DatMagik))
                        continue;
                }
                else if (file.name.EndsWith(".3dt"))
                {
                    if (!System.Text.Encoding.UTF8.GetString(file.data).StartsWith(ThreeDTMagik))
                        continue;
                }
                else
                {
                    continue; // Skip unknown file types
                }

                if (!Directory.Exists(VuforiaPath))
                    Directory.CreateDirectory(VuforiaPath);
                File.WriteAllBytes(Path.Combine(VuforiaPath, file.name), file.data);
            }
        }
#endif

#if UNITY_EDITOR
        private void CollectInternal(UnityEngine.Object sourceAsset)
        {
            vuforiaFiles = Array.Empty<VuforiaFile>();
            
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();

            var dependencies = UnityEditor.AssetDatabase
                .GetDependencies(UnityEditor.AssetDatabase.GetAssetPath(sourceAsset))
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>)
                .ToArray();

            var trackables = dependencies
                .Where(x => x is GameObject)
                .SelectMany(x => ((GameObject)x).GetComponentsInChildren<DataSetTrackableBehaviour>(true))
                .ToDictionary(x => x.TargetName, y => y);
            
            var areaTargets = dependencies
                .Where(x => x is GameObject)
                .SelectMany(x => ((GameObject)x).GetComponentsInChildren<AreaTargetBehaviour>(true))
                .ToDictionary(x => 
                    x.GetType().GetField("OcclusionModelPath", BindingFlags.Instance | BindingFlags.GetField)!.GetValue(x), 
                    y => y);

            if (trackables.Count == 0 && areaTargets.Count == 0) 
                return;
            
            // Scan the StreamingAssets/Vuforia folder for .xml, .dat, and .3dt files
            var vuforiaDatabaseXmlFiles = Directory.GetFiles(VuforiaEditorDatabaseAssetsPath, "*.xml", SearchOption.TopDirectoryOnly);
            var vuforiaDatabaseFiles = Directory.GetFiles(VuforiaEditorDatabaseAssetsPath, "*.dat", SearchOption.TopDirectoryOnly);
            var vuforiaOcclusion3dtFiles = Directory.GetFiles(VuforiaEditorOcclusionAssetsPath, "*.3dt", SearchOption.AllDirectories);
                
            var vuforiaFilesList = new List<VuforiaFile>();
            for (var i = 0; i < vuforiaDatabaseXmlFiles.Length; i++)
            {
                // Usually in the format of <TargetName>.xml
                    
                var vuforiaDatabaseXmlFile = vuforiaDatabaseXmlFiles[i];
                if (!trackables.ContainsKey(Path.GetFileNameWithoutExtension(vuforiaDatabaseXmlFile)))
                    continue;
                    
                var file = new VuforiaFile
                {
                    name = Path.GetFileName(vuforiaDatabaseXmlFile),
                    data = File.ReadAllBytes(vuforiaDatabaseXmlFile)
                };
                vuforiaFilesList.Add(file);
            }
                
            for (var i = 0; i < vuforiaDatabaseFiles.Length; i++)
            {
                // Usually in the format of <TargetName>.dat
                    
                var vuforiaDatabaseFile = vuforiaDatabaseFiles[i];
                if (!trackables.ContainsKey(Path.GetFileNameWithoutExtension(vuforiaDatabaseFile)))
                    continue;
                    
                var file = new VuforiaFile
                {
                    name = Path.GetFileName(vuforiaDatabaseFile),
                    data = File.ReadAllBytes(vuforiaDatabaseFile)
                };
                vuforiaFilesList.Add(file);
            }

            if (areaTargets.Count > 0)
            {
                for (var i = 0; i < vuforiaOcclusion3dtFiles.Length; i++)
                {
                    // Usually in the format of <OcclusionModel>.3dt
                        
                    var vuforiaOcclusion3dtFile = vuforiaOcclusion3dtFiles[i];
                    var file = new VuforiaFile
                    {
                        name = Path.GetFileName(vuforiaOcclusion3dtFile),
                        data = File.ReadAllBytes(vuforiaOcclusion3dtFile)
                    };
                    vuforiaFilesList.Add(file);
                }
            }
                
            vuforiaFiles = vuforiaFilesList.ToArray();
                
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
        
        public static void Clear()
        {
            if (Directory.Exists(VuforiaPath))
                Directory.Delete(VuforiaPath, true);
        }
    }
}