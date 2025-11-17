using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetaverseCloudEngine.Unity.Scripting.Components;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using JetBrains.Annotations;

namespace MetaverseCloudEngine.Unity.Editors
{
    /// <summary>
    /// Utilities for converting Assembly Definition assets (asmdef) into .dll.bytes assemblies
    /// that can be consumed by MetaverseDotNetScript / DotNow at runtime.
    /// </summary>
    internal static class MetaverseDotNetAsmdefBuilder
    {
        private const string MenuPath = "Assets/Metaverse Cloud Engine/.NET Script/Build Assembly From Assembly Definition";
        private const string MetadataResourcesFolder = "Assets/MetaverseDotNetScriptMetadata/Resources/MetaverseDotNetAssemblies";

        [MenuItem(MenuPath, validate = true)]
        private static bool BuildFromAsmdefValidate()
        {
            var obj = Selection.activeObject;
            if (obj == null)
                return false;

            var path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem(MenuPath, priority = 80)]
        private static void BuildFromAsmdef()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning("[METAVERSE_DOTNET_SCRIPT] Scripts are currently compiling. Please wait for compilation to finish and try again.");
                return;
            }

            var obj = Selection.activeObject;
            if (obj == null)
                return;

            var asmdefPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(asmdefPath) || !asmdefPath.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[METAVERSE_DOTNET_SCRIPT] Selected asset is not an Assembly Definition (.asmdef).");
                return;
            }

            // Prevent writing into Library or other non-asset locations.
            if (asmdefPath.StartsWith("Library/", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[METAVERSE_DOTNET_SCRIPT] Cannot build from asmdef located under the Library folder.");
                return;
            }

            var assemblyName = GetAssemblyNameFromAsmdefAssetPath(asmdefPath);
            if (string.IsNullOrEmpty(assemblyName))
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to read assembly name from asmdef at '{asmdefPath}'.");
                return;
            }

            var assembly = FindCompiledAssemblyByName(assemblyName);
            if (assembly == null)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Could not find compiled assembly for '{assemblyName}'. Make sure scripts are compiled successfully.");
                return;
            }

            if (string.IsNullOrEmpty(assembly.outputPath) || !File.Exists(assembly.outputPath))
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Compiled assembly for '{assemblyName}' does not have a valid output path.");
                return;
            }

            byte[] assemblyBytes;
            try
            {
                assemblyBytes = File.ReadAllBytes(assembly.outputPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to read compiled assembly '{assembly.outputPath}': {ex}");
                return;
            }

            // Run security validation on the compiled assembly before writing the .dll.bytes asset.
            if (!MetaverseDotNetScriptSecurity.ValidateAssemblyBytes(assemblyBytes, out var securityMessage))
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Security validation failed for asmdef '{assemblyName}': {securityMessage}");
                return;
            }

            var outputAssetPath = GetOutputAssetPath(asmdefPath, assemblyName);
            var outputAbsolutePath = Path.GetFullPath(outputAssetPath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputAbsolutePath) ?? string.Empty);
                File.WriteAllBytes(outputAbsolutePath, assemblyBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to write assembly asset to '{outputAssetPath}': {ex}");
                return;
            }

            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            CreateOrUpdateMetadataForAssembly(asmdefPath, assemblyName, assemblyBytes, outputAssetPath);

            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(outputAssetPath);
            if (textAsset != null)
            {
                Selection.activeObject = textAsset;
                Debug.Log($"[METAVERSE_DOTNET_SCRIPT] Built .dll.bytes assembly asset for '{assemblyName}' at '{outputAssetPath}'.");
            }
            else
            {
                Debug.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Wrote assembly bytes to '{outputAssetPath}', but Unity did not import it as a TextAsset.");
            }
        }
			internal static bool TryEnsureAssemblyAsset(string asmdefAssetPath, out TextAsset assemblyAsset)
			{
				assemblyAsset = null;

				if (string.IsNullOrEmpty(asmdefAssetPath))
					return false;

				// Prevent writing into Library or other non-asset locations.
				if (asmdefAssetPath.StartsWith("Library/", StringComparison.OrdinalIgnoreCase))
				{
					Debug.LogError("[METAVERSE_DOTNET_SCRIPT] Cannot build from asmdef located under the Library folder.");
					return false;
				}

				var assemblyName = GetAssemblyNameFromAsmdefAssetPath(asmdefAssetPath);
				if (string.IsNullOrEmpty(assemblyName))
					return false;

				var outputAssetPath = GetOutputAssetPath(asmdefAssetPath, assemblyName);
				var outputAbsolutePath = Path.GetFullPath(outputAssetPath);

				// If the asset already exists and can be loaded, just return it.
				if (File.Exists(outputAbsolutePath))
				{
					assemblyAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(outputAssetPath);
					if (assemblyAsset != null)
						return true;
				}

				if (EditorApplication.isCompiling)
				{
					Debug.LogWarning("[METAVERSE_DOTNET_SCRIPT] Scripts are currently compiling. Please wait for compilation to finish and try again.");
					return false;
				}

				var assembly = FindCompiledAssemblyByName(assemblyName);
				if (assembly == null)
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Could not find compiled assembly for '{assemblyName}'. Make sure scripts are compiled successfully.");
					return false;
				}

				if (string.IsNullOrEmpty(assembly.outputPath) || !File.Exists(assembly.outputPath))
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Compiled assembly for '{assemblyName}' does not have a valid output path.");
					return false;
				}

				byte[] assemblyBytes;
				try
				{
					assemblyBytes = File.ReadAllBytes(assembly.outputPath);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to read compiled assembly '{assembly.outputPath}': {ex}");
					return false;
				}

				if (!MetaverseDotNetScriptSecurity.ValidateAssemblyBytes(assemblyBytes, out var securityMessage))
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Security validation failed for asmdef '{assemblyName}': {securityMessage}");
					return false;
				}

				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(outputAbsolutePath) ?? string.Empty);
					File.WriteAllBytes(outputAbsolutePath, assemblyBytes);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to write assembly asset to '{outputAssetPath}': {ex}");
					return false;
				}

				AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
				AssetDatabase.Refresh();

				assemblyAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(outputAssetPath);
				if (!assemblyAsset)
				{
					Debug.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Wrote assembly bytes to '{outputAssetPath}', but Unity did not import it as a TextAsset.");
					return false;
				}

				CreateOrUpdateMetadataForAssembly(asmdefAssetPath, assemblyName, assemblyBytes, outputAssetPath);

				return true;
			}



        private static string GetAssemblyNameFromAsmdefAssetPath(string asmdefAssetPath)
        {
            try
            {
                var absolutePath = Path.GetFullPath(asmdefAssetPath);
                if (!File.Exists(absolutePath))
                    return null;

                var json = File.ReadAllText(absolutePath);
                var data = JsonUtility.FromJson<AsmdefJson>(json);
                return data != null ? data.name : null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to parse asmdef '{asmdefAssetPath}': {ex}");
                return null;
            }
        }

        private static UnityEditor.Compilation.Assembly FindCompiledAssemblyByName(string assemblyName)
        {
            try
            {
                var assemblies = CompilationPipeline.GetAssemblies();
                return assemblies.FirstOrDefault(a => string.Equals(a.name, assemblyName, StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to query compiled assemblies: {ex}");
                return null;
            }
        }

        private static string GetOutputAssetPath(string asmdefAssetPath, string assemblyName)
        {
            var directory = Path.GetDirectoryName(asmdefAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory))
                directory = "Assets";

            var fileName = assemblyName + ".dll.bytes";
            var combined = Path.Combine(directory, fileName);
            return combined.Replace('\\', '/');
        }

        private static string GetMetadataAssetPath(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return null;

            var directory = MetadataResourcesFolder.Replace('\\', '/');
            var fileName = assemblyName + ".asset";
            var combined = Path.Combine(directory, fileName);
            return combined.Replace('\\', '/');
        }

        /// <summary>
        /// Creates or updates the ScriptableObject that stores precomputed hash metadata
        /// for the given DotNow assembly.
        /// </summary>
        private static void CreateOrUpdateMetadataForAssembly(string asmdefAssetPath, string assemblyName, byte[] assemblyBytes, string outputAssetPath)
        {
            if (string.IsNullOrEmpty(asmdefAssetPath))
                return;
            if (string.IsNullOrEmpty(assemblyName))
                return;
            if (assemblyBytes == null || assemblyBytes.Length == 0)
                return;
            if (string.IsNullOrEmpty(outputAssetPath))
                return;

            var hash = MetaverseDotNetScriptCache.ComputeAssemblyHash(assemblyBytes);
            if (string.IsNullOrEmpty(hash))
                return;

            var metadataAssetPath = GetMetadataAssetPath(assemblyName);
            if (string.IsNullOrEmpty(metadataAssetPath))
                return;

            try
            {
                var absolutePath = Path.GetFullPath(metadataAssetPath);
                var directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Failed to ensure metadata directory for '{assemblyName}': {ex}");
                return;
            }

            var metadata = AssetDatabase.LoadAssetAtPath<MetaverseDotNetAssemblyMetadata>(metadataAssetPath);
            if (metadata == null)
            {
                metadata = ScriptableObject.CreateInstance<MetaverseDotNetAssemblyMetadata>();
                metadata.name = assemblyName + " Metadata";
                AssetDatabase.CreateAsset(metadata, metadataAssetPath);
            }

            var assemblyTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(outputAssetPath);
            if (!assemblyTextAsset)
            {
                AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
                assemblyTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(outputAssetPath);
            }

            if (!assemblyTextAsset)
            {
                Debug.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Failed to create metadata for '{assemblyName}' because the .dll.bytes asset could not be loaded.");
                return;
            }

            metadata.SetEditorData(assemblyTextAsset, hash, asmdefAssetPath, assemblyName);
            AssetDatabase.SaveAssets();
        }

        [Serializable]
        private class AsmdefJson
        {
            [UsedImplicitly]
            #pragma warning disable CS0649
            public string name;
            #pragma warning restore CS0649
        }

        // --- Auto-rebuild support --------------------------------------------------------------
        private static readonly HashSet<string> _pendingAsmdefs = new HashSet<string>();
        private static bool _isSubscribedToCompilation;

        /// <summary>
        /// AssetPostprocessor hook that watches for changes to .cs and .asmdef files
        /// and schedules auto-rebuilds for asmdefs that already have a corresponding
        /// .dll.bytes asset.
        /// </summary>
        private class AutoRebuildAssetPostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                OnAssetsChanged(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            }
        }

        private static void OnAssetsChanged(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (EditorApplication.isUpdating)
                return;

            var anyQueued = false;

            // We only care about .cs and .asmdef changes that live under Assets/.
            IEnumerable<string> candidates = Enumerable.Empty<string>();
            if (importedAssets != null && importedAssets.Length > 0)
                candidates = candidates.Concat(importedAssets);
            if (deletedAssets != null && deletedAssets.Length > 0)
                candidates = candidates.Concat(deletedAssets);
            if (movedAssets != null && movedAssets.Length > 0)
                candidates = candidates.Concat(movedAssets);
            if (movedFromAssetPaths != null && movedFromAssetPaths.Length > 0)
                candidates = candidates.Concat(movedFromAssetPaths);

            var candidateList = candidates
                .Where(p => !string.IsNullOrEmpty(p) && p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidateList.Count == 0)
                return;

            // Process asmdef changes directly.
            foreach (var path in candidateList)
            {
                if (path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                {
                    if (QueueIfHasExistingBytes(path))
                        anyQueued = true;
                }
            }

            // Map script changes back to their owning asmdefs via CompilationPipeline.
            var assemblies = CompilationPipeline.GetAssemblies();
            foreach (var path in candidateList)
            {
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var assembly in assemblies)
                {
                    if (assembly.sourceFiles == null)
                        continue;

                    if (!assembly.sourceFiles.Contains(path))
                        continue;

                    var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                    if (string.IsNullOrEmpty(asmdefPath))
                        continue;

                    if (QueueIfHasExistingBytes(asmdefPath))
                        anyQueued = true;
                }
            }

            if (anyQueued)
                EnsureCompilationHooked();
        }

        /// <summary>
        /// Adds the given asmdef to the rebuild queue only if a .dll.bytes asset already
        /// exists for it. This ensures we only auto-build assemblies that have been
        /// explicitly opted into the DotNow pipeline once.
        /// </summary>
        private static bool QueueIfHasExistingBytes(string asmdefAssetPath)
        {
            var assemblyName = GetAssemblyNameFromAsmdefAssetPath(asmdefAssetPath);
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            var outputAssetPath = GetOutputAssetPath(asmdefAssetPath, assemblyName);
            var outputAbsolutePath = Path.GetFullPath(outputAssetPath);
            if (!File.Exists(outputAbsolutePath))
                return false;

            lock (_pendingAsmdefs)
            {
                if (_pendingAsmdefs.Add(asmdefAssetPath))
                    return true;
            }

            return false;
        }

        private static void EnsureCompilationHooked()
        {
            if (_isSubscribedToCompilation)
                return;

            _isSubscribedToCompilation = true;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        private static void OnCompilationFinished(object _)
        {
            _isSubscribedToCompilation = false;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;

            string[] asmdefs;
            lock (_pendingAsmdefs)
            {
                asmdefs = _pendingAsmdefs.ToArray();
                _pendingAsmdefs.Clear();
            }

            if (asmdefs.Length == 0)
                return;

            foreach (var asmdefPath in asmdefs)
            {
                AutoRebuildAsmdef(asmdefPath);
            }
        }

        private static void AutoRebuildAsmdef(string asmdefAssetPath)
        {
            var assemblyName = GetAssemblyNameFromAsmdefAssetPath(asmdefAssetPath);
            if (string.IsNullOrEmpty(assemblyName))
                return;

            var assembly = FindCompiledAssemblyByName(assemblyName);
            if (assembly == null)
            {
                Debug.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Auto-rebuild skipped for '{assemblyName}' because no compiled assembly was found.");
                return;
            }

            if (string.IsNullOrEmpty(assembly.outputPath) || !File.Exists(assembly.outputPath))
            {
                Debug.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Auto-rebuild skipped for '{assemblyName}' because the compiled assembly output path is invalid.");
                return;
            }

            byte[] assemblyBytes;
            try
            {
                assemblyBytes = File.ReadAllBytes(assembly.outputPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Auto-rebuild failed to read compiled assembly '{assembly.outputPath}': {ex}");
                return;
            }

            if (!MetaverseDotNetScriptSecurity.ValidateAssemblyBytes(assemblyBytes, out var securityMessage))
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Auto-rebuild security validation failed for asmdef '{assemblyName}': {securityMessage}");
                return;
            }

            var outputAssetPath = GetOutputAssetPath(asmdefAssetPath, assemblyName);
            var outputAbsolutePath = Path.GetFullPath(outputAssetPath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputAbsolutePath) ?? string.Empty);
                File.WriteAllBytes(outputAbsolutePath, assemblyBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Auto-rebuild failed to write assembly asset to '{outputAssetPath}': {ex}");
                return;
            }

            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);

            CreateOrUpdateMetadataForAssembly(asmdefAssetPath, assemblyName, assemblyBytes, outputAssetPath);

            Debug.Log($"[METAVERSE_DOTNET_SCRIPT] Auto-rebuilt {Path.GetFileName(outputAssetPath)}.");
        }

    }
}

