using System;
using System.IO;
using TriInspectorMVCE;
using UnityEngine;
using Vuforia;

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

        public static string VuforiaPath => Path.Combine(Application.temporaryCachePath, "VuforiaStreamingAssets");
        
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
        public static void Collect()
        {
            if (!Instance)
                return;
            
            Instance.CollectInternal();
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

#if UNITY_EDITOR
        [Button("Detect Files")]
        private void CollectInternal()
        {
            vuforiaFiles = Array.Empty<VuforiaFile>();
            
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            
            // Scan the StreamingAssets/Vuforia folder for .xml, .dat, and .3dt files
            var vuforiaDatabaseXmlFiles = Directory.GetFiles(VuforiaPath, "*.xml", SearchOption.TopDirectoryOnly);
            var vuforiaDatabaseFiles = Directory.GetFiles(VuforiaPath, "*.dat", SearchOption.TopDirectoryOnly);
            var vuforia3dtFiles = Directory.GetFiles(VuforiaPath, "*.3dt", SearchOption.TopDirectoryOnly);
            
            vuforiaFiles = new VuforiaFile[vuforiaDatabaseXmlFiles.Length + vuforiaDatabaseFiles.Length + vuforia3dtFiles.Length];
            var index = 0;
            for (var i = 0; i < vuforiaDatabaseXmlFiles.Length; i++)
            {
                var file = new VuforiaFile
                {
                    name = Path.GetFileName(vuforiaDatabaseXmlFiles[i]),
                    data = File.ReadAllBytes(vuforiaDatabaseXmlFiles[i])
                };
                vuforiaFiles[index++] = file;
            }
            
            for (var i = 0; i < vuforiaDatabaseFiles.Length; i++)
            {
                var file = new VuforiaFile
                {
                    name = Path.GetFileName(vuforiaDatabaseFiles[i]),
                    data = File.ReadAllBytes(vuforiaDatabaseFiles[i])
                };
                vuforiaFiles[index++] = file;
            }
            
            for (var i = 0; i < vuforia3dtFiles.Length; i++)
            {
                var file = new VuforiaFile
                {
                    name = Path.GetFileName(vuforia3dtFiles[i]),
                    data = File.ReadAllBytes(vuforia3dtFiles[i])
                };
                vuforiaFiles[index++] = file;
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
        
        public static void Clear()
        {
            if (Directory.Exists(VuforiaPath))
                Directory.Delete(VuforiaPath, true);
        }
#endif
    }
}