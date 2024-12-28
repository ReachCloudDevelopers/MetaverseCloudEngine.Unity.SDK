using System;
using System.Linq;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors.Builds
{
    public interface IFinalizeGradleBundle : IOrderedCallback
    {
        void FinalizeGradleBundle(string path);
    }
    
    public class FinalizeGradleBundleInitializer : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get; } = int.MaxValue;
        
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x =>
                {
                    try { return x.GetTypes(); }
                    catch (Exception e) { return Array.Empty<Type>(); }
                }).Where(x =>
                {
                    try { return typeof(IFinalizeGradleBundle).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract; }
                    catch (Exception e) { return false; }
                }).ToArray();

            foreach (var type in types)
            {
                try
                {
                    var instance = (IFinalizeGradleBundle) Activator.CreateInstance(type);
                    instance.FinalizeGradleBundle(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to finalize Gradle bundle with '{type.Name}': {e}");
                }
            }
        }
    }
}