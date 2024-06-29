using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Vuforia;

namespace MetaverseCloudEngine.Unity.Vuforia
{
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-int.MaxValue)]
    [DisallowMultipleComponent]
    public class VuforiaAreaTargetConfigurationHelper : MonoBehaviour
    {
        private void Awake()
        {
            var areaTargetBehavior = GetComponent<AreaTargetBehaviour>();
            if (!areaTargetBehavior)
                return;

            var loader = FindObjectOfType<VuforiaStreamingAssetsLoader>(true);
            if (loader == null)
                throw new Exception("No VuforiaStreamingAssetsLoader found in scene.");

            loader.RunOnAwake();
            
            // Use reflection to modify:
            // 1. DataSetPath to absolute path
            // 2. OcclusionModelStorageType = StorageType.ABSOLUTE
            // 3. OcclusionModelPath to absolute path
            
            var areaTargetBehaviorType = areaTargetBehavior.GetType();
            const BindingFlags internalPropertyFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty | BindingFlags.SetProperty;
            const BindingFlags internalFieldFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField;
            
            var dataSetPathField = areaTargetBehaviorType.GetProperty("DataSetPath", internalPropertyFlags) ?? throw new MissingFieldException("DataSetPath");
            var occlusionModelStorageTypeField = areaTargetBehaviorType.GetField("OcclusionModelStorageType", internalFieldFlags) ?? throw new MissingFieldException("OcclusionModelStorageType");
            var occlusionModelPathField = areaTargetBehaviorType.GetField("OcclusionModelPath", internalFieldFlags) ?? throw new MissingFieldException("OcclusionModelPath");
            
            occlusionModelStorageTypeField!.SetValue(areaTargetBehavior, StorageType.ABSOLUTE);

            var dataSetPath = (string)dataSetPathField!.GetValue(areaTargetBehavior);
            if (!string.IsNullOrEmpty(dataSetPath))
            {
                var vuforiaPath = $"{VuforiaStreamingAssets.VuforiaPath}{Path.DirectorySeparatorChar}{Path.GetFileName(dataSetPath)}";
                if (!File.Exists(vuforiaPath))
                    throw new FileNotFoundException("Vuforia dataset not found at " + vuforiaPath);
                dataSetPathField.SetValue(areaTargetBehavior, vuforiaPath);
            }
            
            var occlusionModelPath = (string)occlusionModelPathField!.GetValue(areaTargetBehavior);
            if (!string.IsNullOrEmpty(occlusionModelPath))
            {
                var vuforiaPath = $"{VuforiaStreamingAssets.VuforiaPath}{Path.DirectorySeparatorChar}{Path.GetFileName(occlusionModelPath)}";
                if (!File.Exists(vuforiaPath))
                    throw new FileNotFoundException("Vuforia occlusion model not found at " + vuforiaPath);
                occlusionModelPathField.SetValue(areaTargetBehavior, vuforiaPath);
            }
            
            MetaverseProgram.Logger.Log("VuforiaAreaTargetConfigurationHelper: Awake() completed on " + areaTargetBehavior.name);
        }
    }
}