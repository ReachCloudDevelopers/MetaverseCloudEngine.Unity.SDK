﻿using Esprima.Ast;

using Jint;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// A type used by <see cref="MetaverseScript"/> that allows caching of data
    /// for the current scene. Will be destroyed upon scene load.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class MetaverseScriptCache : MonoBehaviour
    {
        private Dictionary<TextAsset, Script> _scriptModules;
        private Dictionary<string, object> _staticReferences;
        private static MetaverseScriptCache _current;

        /// <summary>
        /// Gets the current script cache object for the active scene.
        /// </summary>
        public static MetaverseScriptCache Current {
            get {
                if (!Application.isPlaying)
                    return null;
                if (_current)
                    return _current;
                _current = MVUtils.FindObjectsOfTypeNonPrefabPooled<MetaverseScriptCache>(true).FirstOrDefault();
                if (_current)
                    return _current;
                _current = new GameObject(MVUtils.GenerateUid()).AddComponent<MetaverseScriptCache>();
                _current.gameObject.hideFlags = Application.isPlaying ? (HideFlags.HideInHierarchy | HideFlags.NotEditable) : HideFlags.HideAndDontSave;
                return _current;
            }
        }

        /// <summary>
        /// Gets a cached Esprima script object or creates one if it's not already cached.
        /// </summary>
        /// <param name="asset">The text asset that contians the source code.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <param name="preProcessScript">An optional preprocessing step to run on the text of the source file.</param>
        /// <returns>The <see cref="Script"/> that was prepared by <see cref="Engine.PrepareScript(string, string?, bool)"/></returns>
        public UniTask<Script> GetScriptAsync(TextAsset asset, CancellationToken cancellationToken, Func<string, string> preProcessScript = null)
        {
            if (!asset)
                throw new ArgumentNullException(nameof(asset));
            return UniTask.Create(async () =>
            {
                await UniTask.SwitchToMainThread(cancellationToken);
                _scriptModules ??= new Dictionary<TextAsset, Script>();
                if (_scriptModules.TryGetValue(asset, out var code))
                    return code;
                MetaverseProgram.Logger.Log($"[MetaverseScript] Initializing {asset.name}.js...");
                var text = asset.text;
                if (preProcessScript != null)
                    text = preProcessScript?.Invoke(text);
                if (Application.platform != RuntimePlatform.WebGLPlayer)
                    await UniTask.SwitchToThreadPool();
                var script = Engine.PrepareScript(text, strict: true);
                await UniTask.SwitchToMainThread(cancellationToken);
                return _scriptModules[asset] = script;
            });
        }

        /// <summary>
        /// Sets a static reference in the current scene.
        /// </summary>
        /// <param name="key">The key used to lookup the static object.</param>
        /// <param name="value">The value to apply to the reference.</param>
        public void SetStaticReference(string key, object value)
        {
            _staticReferences ??= new Dictionary<string, object>();
            _staticReferences[key] = value;
        }

        /// <summary>
        /// Gets a static reference by it's <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The lookup <paramref name="key"/> to use for retrieving the static reference.</param>
        /// <returns>The value of the static reference, if any.</returns>
        public object GetStaticReference(string key)
        {
            return _staticReferences?.GetValueOrDefault(key);
        }
    }
}
