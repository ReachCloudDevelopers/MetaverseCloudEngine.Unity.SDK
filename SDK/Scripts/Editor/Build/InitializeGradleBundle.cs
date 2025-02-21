#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor.Android;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors.Builds
{
    internal class InitializeGradleBundle : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get; } = -int.MaxValue;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x =>
                {
                    try { return x.GetTypes(); }
                    catch (Exception e) { return Array.Empty<Type>(); }
                }).Where(x =>
                {
                    try { return typeof(IInitializeGradleBundle).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract; }
                    catch (Exception e) { return false; }
                }).ToArray();
            
            var instances = types.Select(type =>
            {
                try
                {
                    return (IInitializeGradleBundle) Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to finalize Gradle bundle with '{type.Name}': {e}");
                    return null;
                }
            }).Where(x => x != null).OrderBy(x => x.callbackOrder).ToArray();

            foreach (var instance in instances)
            {
                try
                {
                    instance.InitializeGradleBundle(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to finalize Gradle bundle with '{instance.GetType().Name}': {e}");
                }
            }
        }
    }
}
#endif