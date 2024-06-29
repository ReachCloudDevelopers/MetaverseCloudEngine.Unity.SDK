using System;
using System.Reflection;
using UnityEngine;
using Vuforia;

namespace MetaverseCloudEngine.Unity.Vuforia
{
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-int.MaxValue)]
    public class VuforiaAreaTargetConfigurationHelper : MonoBehaviour
    {
        private void Awake()
        {
            var areaTargetBehavior = GetComponent<AreaTargetBehaviour>();
            if (!areaTargetBehavior)
                return;
            
            // Use reflection to modify:
            // 1. DataSetPath to absolute path
            // 2. OcclusionModelStorageType = StorageType.ABSOLUTE
            // 3. OcclusionModelPath to absolute path
            
            var areaTargetBehaviorType = areaTargetBehavior.GetType();
            const BindingFlags internalPropertyFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty | BindingFlags.SetProperty;
            const BindingFlags internalFieldFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField;
            var dataSetPathField = areaTargetBehaviorType.GetProperty("DataSetPath", internalPropertyFlags);
            var occlusionModelStorageTypeField = areaTargetBehaviorType.GetField("OcclusionModelStorageType", internalFieldFlags);
            var occlusionModelPathField = areaTargetBehaviorType.GetField("OcclusionModelPath", internalFieldFlags);
            
            if (occlusionModelStorageTypeField != null)
                occlusionModelStorageTypeField.SetValue(areaTargetBehavior, StorageType.ABSOLUTE);

            if (dataSetPathField != null)
            {
                var dataSetPath = (string)dataSetPathField.GetValue(areaTargetBehavior);
                if (!string.IsNullOrEmpty(dataSetPath))
                    dataSetPathField.SetValue(areaTargetBehavior, VuforiaStreamingAssets.VuforiaPath + "/" + dataSetPath);
            }
            
            if (occlusionModelPathField != null)
            {
                var occlusionModelPath = (string)occlusionModelPathField.GetValue(areaTargetBehavior);
                if (!string.IsNullOrEmpty(occlusionModelPath))
                    occlusionModelPathField.SetValue(areaTargetBehavior, VuforiaStreamingAssets.VuforiaPath + "/" + occlusionModelPath);
            }
            
            MetaverseProgram.Logger.Log("VuforiaAreaTargetConfigurationHelper: Awake() completed on " + areaTargetBehavior.name);
        }
    }
}