#if MV_DOTNOW_SCRIPTING

using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// ScriptableObject that stores precomputed hash metadata for a DotNow-compatible
    /// .dll.bytes assembly. Instances are generated/updated by MetaverseDotNetAsmdefBuilder
    /// and discovered at runtime to avoid re-hashing identical assemblies.
    /// </summary>
    public class MetaverseDotNetAssemblyMetadata : ScriptableObject
    {
        [SerializeField]
        private TextAsset assemblyAsset;

        [SerializeField]
        private string assemblyHash;

        // Optional editor-only bookkeeping to help trace where the assembly came from.
        #if UNITY_EDITOR
        [SerializeField]
        private string asmdefAssetPath;

        [SerializeField]
        private string assemblyName;
        #endif

        private static readonly Dictionary<TextAsset, string> HashByAsset = new();
        private static bool _loaded;

        internal TextAsset AssemblyAsset => assemblyAsset;
        internal string AssemblyHash => assemblyHash;

        private void OnEnable()
        {
            Register();
        }

        private void Register()
        {
            if (!assemblyAsset || string.IsNullOrEmpty(assemblyHash))
                return;

            HashByAsset[assemblyAsset] = assemblyHash;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;

            #if UNITY_EDITOR
            try
            {
                var guids = AssetDatabase.FindAssets("t:MetaverseDotNetAssemblyMetadata");
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    var metadata = AssetDatabase.LoadAssetAtPath<MetaverseDotNetAssemblyMetadata>(path);
                    if (metadata)
                        metadata.Register();
                }
            }
            catch (Exception)
            {
                // Ignore editor lookup failures; runtime will still be able to compute hashes on demand.
            }
            #else
            try
            {
                // Runtime lookup: metadata assets are expected to live under a Resources folder
                // (e.g. Assets/MetaverseDotNetScriptMetadata/Resources/MetaverseDotNetAssemblies).
                var assets = Resources.LoadAll<MetaverseDotNetAssemblyMetadata>("MetaverseDotNetAssemblies");
                for (var i = 0; i < assets.Length; i++)
                {
                    var metadata = assets[i];
                    if (metadata)
                        metadata.Register();
                }
            }
            catch
            {
                // Ignore runtime lookup failures; we can still fall back to hashing bytes.
            }
            #endif
        }

        /// <summary>
        /// Attempts to get a precomputed hash for the given assembly TextAsset.
        /// </summary>
        internal static bool TryGetHash(TextAsset asset, out string hash)
        {
            if (!asset)
            {
                hash = null;
                return false;
            }

            EnsureLoaded();
            return HashByAsset.TryGetValue(asset, out hash) && !string.IsNullOrEmpty(hash);
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Editor-only helper used by MetaverseDotNetAsmdefBuilder to create or update
        /// metadata instances when asmdefs are built.
        /// </summary>
        public void SetEditorData(TextAsset assemblyTextAsset, string hash, string asmdefPath, string assemblyName)
        {
            assemblyAsset = assemblyTextAsset;
            assemblyHash = hash;
            asmdefAssetPath = asmdefPath;
            this.assemblyName = assemblyName;

            Register();
            EditorUtility.SetDirty(this);
        }
        #endif
    }
}


#endif // MV_DOTNOW_SCRIPTING
