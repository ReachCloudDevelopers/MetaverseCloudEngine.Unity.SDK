#if MV_DOTNOW_SCRIPTING

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// Caches DotNow AppDomain and loaded assemblies for MetaverseDotNetScript instances in the current scene.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class MetaverseDotNetScriptCache : MonoBehaviour
    {
        private readonly Dictionary<TextAsset, Assembly> _assemblies = new();
        private readonly Dictionary<string, Assembly> _assembliesByHash = new(StringComparer.Ordinal);
        private static MetaverseDotNetScriptCache _current;

        private object _appDomain;
        private Type _appDomainType;
        private Type _appDomainOptionsType;
        private Type _clrInstanceType;
        private MethodInfo _loadAssemblyStream;
        private MethodInfo _createInstance;
        private MethodInfo _unwrapAsType;
        private readonly object _lock = new();
        private static bool _loggedMissingDotNow;

        /// <summary>
        /// Gets the current cache instance for the active scene.
        /// </summary>
        public static MetaverseDotNetScriptCache Current {
            get {
                if (!Application.isPlaying)
                    return null;
                if (_current)
                    return _current;
                _current = MVUtils.FindObjectsOfTypeNonPrefabPooled<MetaverseDotNetScriptCache>(true).FirstOrDefault();
                if (_current)
                    return _current;
                _current = new GameObject(MVUtils.GenerateUid()).AddComponent<MetaverseDotNetScriptCache>();
                _current.gameObject.hideFlags = Application.isPlaying
                    ? HideFlags.HideInHierarchy | HideFlags.NotEditable
                    : HideFlags.HideAndDontSave;
                return _current;
            }
        }

        private bool EnsureAppDomain()
        {
            if (_appDomain != null)
                return true;

            // Resolve DotNow types without hard-coding the assembly name so this works
            // regardless of how the package is compiled/loaded by Unity.
            _appDomainType ??= ResolveDotNowType("dotnow.AppDomain");
            if (_appDomainType == null)
            {
                if (!_loggedMissingDotNow)
                {
                    _loggedMissingDotNow = true;
                    MetaverseProgram.Logger.LogError("[METAVERSE_DOTNET_SCRIPT] dotnow.AppDomain type could not be found. Ensure 'com.scottyboy805.dotnow' is installed.");
                }
                return false;
            }

            _appDomainOptionsType ??= ResolveDotNowType("dotnow.AppDomainOptions");
            object optionsArg = null;
            if (_appDomainOptionsType != null)
            {
                try
                {
                    // Use default options (0) if available. This keeps behaviour aligned with the core project.
                    optionsArg = Enum.ToObject(_appDomainOptionsType, 0u);
                }
                catch
                {
                    optionsArg = null;
                }
            }

            try
            {
                _appDomain = optionsArg != null
                    ? Activator.CreateInstance(_appDomainType, optionsArg)
                    : Activator.CreateInstance(_appDomainType);
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to create DotNow AppDomain: {ex}");
                _appDomain = null;
                return false;
            }

            _clrInstanceType ??= ResolveDotNowType("dotnow.Interop.ICLRInstance");
            _loadAssemblyStream ??= _appDomainType.GetMethod("LoadAssemblyStream", new[] { typeof(Stream), typeof(Stream), typeof(bool) });
            _createInstance ??= _appDomainType.GetMethod("CreateInstance", new[] { typeof(Type), typeof(object[]) });
            if (_clrInstanceType != null)
                _unwrapAsType ??= _clrInstanceType.GetMethod("UnwrapAsType", new[] { typeof(Type) });

            if (_appDomain == null || _loadAssemblyStream == null || _createInstance == null)
            {
                MetaverseProgram.Logger.LogError("[METAVERSE_DOTNET_SCRIPT] Failed to bind required DotNow reflection members.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates an instance of the specified script type from the given assembly asset.
        /// </summary>
        public bool TryCreateScriptInstance(TextAsset assemblyAsset, string className, out IMetaverseDotNetScript instance)
        {
            instance = null;
            if (!assemblyAsset || string.IsNullOrWhiteSpace(className))
                return false;

            if (!EnsureAppDomain())
                return false;

            Assembly assembly;
            lock (_lock)
            {
                if (!_assemblies.TryGetValue(assemblyAsset, out assembly))
                {
                    // Try to resolve a stable identity for this assembly. We prefer a precomputed
                    // hash (from build-time metadata) but will fall back to hashing the bytes
                    // directly when metadata is not available.
                    var hash = GetAssemblyHash(assemblyAsset);

                    if (!string.IsNullOrEmpty(hash) && _assembliesByHash.TryGetValue(hash, out var existingAssembly))
                    {
                        assembly = existingAssembly;
                        _assemblies[assemblyAsset] = assembly;
                    }
                    else
                    {
                        // Validate security rules before caching/using the assembly. Use the raw assembly bytes
                        // (loaded via System.Reflection) rather than the DotNow-wrapped assembly to avoid
                        // partial reflection implementations throwing NotImplementedException.
                        if (!MetaverseDotNetScriptSecurity.ValidateAssemblyBytes(assemblyAsset.bytes, out var securityMessage))
                        {
                            MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Security validation failed for assembly '{assemblyAsset.name}': {securityMessage}");
                            return false;
                        }

                        try
                        {
                            using var stream = new MemoryStream(assemblyAsset.bytes, writable: false);
                            assembly = (Assembly)_loadAssemblyStream.Invoke(_appDomain, new object[] { stream, null, false });

                            _assemblies[assemblyAsset] = assembly;

                            if (!string.IsNullOrEmpty(hash))
                                _assembliesByHash[hash] = assembly;
                        }
                        catch (Exception ex)
                        {
                            MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to load assembly for '{assemblyAsset.name}': {ex}");
                            return false;
                        }
                    }
                }
            }

            // If the assembly was already cached or reused, ensure it still passes security validation
            // (in case rules changed since it was originally loaded).
            if (!MetaverseDotNetScriptSecurity.ValidateAssemblyBytes(assemblyAsset.bytes, out var cachedSecurityMessage))
            {
                MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Security validation failed for cached assembly '{assemblyAsset.name}': {cachedSecurityMessage}");
                return false;
            }

            Type type;
            try
            {
                type = assembly.GetType(className, throwOnError: false, ignoreCase: false);
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to resolve type '{className}' in assembly '{assemblyAsset.name}': {ex}");
                return false;
            }

            if (type == null)
            {
                MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Type '{className}' was not found in assembly '{assemblyAsset.name}'.");
                return false;
            }

            var interfaceFullName = typeof(IMetaverseDotNetScript).FullName;
            var baseFullName = typeof(MetaverseDotNetScriptBase).FullName;

            if (!ImplementsDotNetScriptContract(type, interfaceFullName, baseFullName))
            {
                MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Type '{className}' does not implement IMetaverseDotNetScript.");
                return false;
            }

            object rawInstance;
            try
            {
                rawInstance = _createInstance.Invoke(_appDomain, new object[] { type, Array.Empty<object>() });
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to create instance of '{className}': {ex}");
                return false;
            }

            if (rawInstance is IMetaverseDotNetScript direct)
            {
                instance = direct;
                return true;
            }

            if (_clrInstanceType != null && _unwrapAsType != null && _clrInstanceType.IsInstanceOfType(rawInstance))
            {
                try
                {
                    var unwrapped = _unwrapAsType.Invoke(rawInstance, new object[] { typeof(IMetaverseDotNetScript) });
                    if (unwrapped is IMetaverseDotNetScript wrapped)
                    {
                        instance = wrapped;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to unwrap ICLRInstance for '{className}': {ex}");
                }
            }

            MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Created instance of '{className}' but could not cast it to IMetaverseDotNetScript.");
            return false;
        }

		public void ApplySerializedFields(MetaverseDotNetScript component, IMetaverseDotNetScript scriptInstance)
		{
		    if (component == null || scriptInstance == null)
		        return;

		    var fields = component.SerializedFields;
		    if (fields == null || fields.Count == 0)
		        return;

		    var assemblyAsset = component.AssemblyAsset;
		    var className = component.ClassName;

		    if (!assemblyAsset || string.IsNullOrEmpty(className))
		        return;

		    if (!_assemblies.TryGetValue(assemblyAsset, out var assembly) || assembly == null)
		        return;

		    Type scriptType;
		    try
		    {
		        scriptType = assembly.GetType(className, throwOnError: false, ignoreCase: false);
		    }
		    catch (Exception ex)
		    {
		        MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to resolve type '{className}' for applying serialized fields: {ex}");
		        return;
		    }

		    if (scriptType == null)
		        return;

		    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		    for (var i = 0; i < fields.Count; i++)
		    {
		        var sf = fields[i];
		        if (sf == null || !sf.hasOverride || string.IsNullOrEmpty(sf.fieldName))
		            continue;

		        var declaringType = ResolveDeclaringType(scriptType, sf.declaringTypeName);
		        if (declaringType == null)
		            continue;

		        FieldInfo fieldInfo = null;
		        try
		        {
		            fieldInfo = declaringType.GetField(sf.fieldName, flags);
		        }
		        catch (Exception ex)
		        {
		            MetaverseProgram.Logger.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Failed to resolve field '{sf.fieldName}' on '{declaringType.FullName}': {ex.Message}");
		        }

		        if (fieldInfo == null)
		            continue;

		        object value;
		        try
		        {
		            value = ConvertSerializedFieldValue(sf, fieldInfo.FieldType);
		        }
		        catch (Exception ex)
		        {
		            MetaverseProgram.Logger.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Failed to convert value for field '{sf.fieldName}' on '{declaringType.FullName}': {ex.Message}");
		            continue;
		        }

		        try
		        {
		            fieldInfo.SetValue(scriptInstance, value);
		        }
		        catch (Exception ex)
		        {
		            MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to apply value to field '{sf.fieldName}' on '{declaringType.FullName}': {ex}");
		        }
		    }
		}

		public void NotifyScriptInstanceDestroyed(IMetaverseDotNetScript scriptInstance)
		{
		    // Intentionally left blank. We do not currently track per-instance state in the cache.
		    _ = scriptInstance;
		}

		private static Type ResolveDeclaringType(Type scriptType, string declaringTypeFullName)
		{
		    if (scriptType == null)
		        return null;

		    if (string.IsNullOrEmpty(declaringTypeFullName) || scriptType.FullName == declaringTypeFullName)
		        return scriptType;

		    var cursor = scriptType;
		    while (cursor != null && cursor != typeof(object))
		    {
		        if (cursor.FullName == declaringTypeFullName)
		            return cursor;

		        cursor = cursor.BaseType;
		    }

		    return scriptType;
		}

		private static object ConvertSerializedFieldValue(MetaverseDotNetScript.SerializedField sf, Type fieldType)
		{
		    if (fieldType == null || sf == null)
		        return null;

		    // Enum fields are stored as int in the serialized data.
		    if (fieldType.IsEnum)
		    {
		        try
		        {
		            return Enum.ToObject(fieldType, sf.intValue);
		        }
		        catch
		        {
		            return null;
		        }
		    }

		    if (fieldType == typeof(bool))
		        return sf.boolValue;

		    if (fieldType == typeof(int))
		        return sf.intValue;

		    if (fieldType == typeof(float))
		        return sf.floatValue;

		    if (fieldType == typeof(string))
		        return sf.stringValue;

		    if (fieldType == typeof(Vector2))
		        return sf.vector2Value;

		    if (fieldType == typeof(Vector3))
		        return sf.vector3Value;

		    if (fieldType == typeof(Color))
		        return sf.colorValue;

		    if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
		    {
		        if (sf.objectReference == null)
		            return null;

		        if (fieldType.IsInstanceOfType(sf.objectReference))
		            return sf.objectReference;

		        return null;
		    }

		    // Fallback: attempt simple conversion from the stored string if available.
		    if (!string.IsNullOrEmpty(sf.stringValue))
		    {
		        try
		        {
		            return Convert.ChangeType(sf.stringValue, fieldType);
		        }
		        catch
		        {
		            // ignored
		        }
		    }

		    return null;
		}


        /// <summary>
        /// Attempts to resolve a stable hash for the given assembly asset using precomputed
        /// metadata when available, falling back to hashing the bytes directly.
        /// </summary>
        private static string GetAssemblyHash(TextAsset assemblyAsset)
        {
            if (!assemblyAsset)
                return null;

            // Prefer build-time metadata so we only pay the hashing cost once per assembly.
            if (MetaverseDotNetAssemblyMetadata.TryGetHash(assemblyAsset, out var metadataHash) && !string.IsNullOrEmpty(metadataHash))
                return metadataHash;

            try
            {
                return ComputeAssemblyHash(assemblyAsset.bytes);
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Failed to compute assembly hash for '{assemblyAsset.name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Computes a SHA-256 hash for the given assembly bytes and returns it as an
        /// uppercase hex string without separators. This is used as a stable identity
        /// for deduplicating physically identical assemblies within a DotNow AppDomain.
        /// </summary>
        public static string ComputeAssemblyHash(byte[] assemblyBytes)
        {
            if (assemblyBytes == null || assemblyBytes.Length == 0)
                return null;

            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(assemblyBytes);
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
        }

        private static Type ResolveDotNowType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return null;

            // Known assembly names for the DotNow runtime. The project was originally called
            // "Trivial CLR" and some versions still use that assembly name.
            var candidateAssemblies = new[] { "dotnow", "TrivialCLR" };

            // 1. Try simple lookup first.
            try
            {
                var direct = Type.GetType(fullName, throwOnError: false);
                if (direct != null)
                    return direct;
            }
            catch
            {
                // ignored
            }

            // 2. Try with explicit assembly qualifiers to force-load the runtime if needed.
            for (var i = 0; i < candidateAssemblies.Length; i++)
            {
                var asmName = candidateAssemblies[i];
                if (string.IsNullOrWhiteSpace(asmName))
                    continue;

                try
                {
                    var qualified = $"{fullName}, {asmName}";
                    var type = Type.GetType(qualified, throwOnError: false);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // ignored
                }
            }

            // 3. Fallback: scan all currently loaded assemblies to find the type by full name.
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (assembly == null || assembly.IsDynamic)
                    continue;

                try
                {
                    var type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // ignored
                }
            }

            return null;
        }

        private static bool ImplementsDotNetScriptContract(Type type, string interfaceFullName, string baseFullName)
        {
            if (type == null)
                return false;

            // Fast path when DotNow returns a CLR Type that shares the same load context
            try
            {
                if (typeof(IMetaverseDotNetScript).IsAssignableFrom(type))
                    return true;
            }
            catch
            {
                // Ignore and fall back to name-based checks below.
            }

            // Fallback 1: interface match by full name (avoids load-context identity issues)
            try
            {
                if (type.GetInterfaces().Any(i => i.FullName == interfaceFullName))
                    return true;
            }
            catch
            {
                // ignored
            }

            // Fallback 2: base-type chain match by full name (for MetaverseDotNetScriptBase subclasses)
            try
            {
                var baseType = type.BaseType;
                while (baseType != null)
                {
                    if (baseType.FullName == baseFullName)
                        return true;

                    baseType = baseType.BaseType;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private void OnDestroy()
        {
            if (_appDomain != null)
            {
                try
                {
                    var dispose = _appDomainType?.GetMethod("Dispose", Type.EmptyTypes);
                    dispose?.Invoke(_appDomain, null);
                }
                catch (Exception ex)
                {
                    MetaverseProgram.Logger.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Error while disposing DotNow AppDomain: {ex}");
                }
                finally
                {
                    _appDomain = null;
                }
            }

            if (ReferenceEquals(_current, this))
                _current = null;
        }
    }
}


#endif // MV_DOTNOW_SCRIPTING
