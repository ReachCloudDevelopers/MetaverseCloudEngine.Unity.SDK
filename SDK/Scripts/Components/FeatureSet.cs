using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component that enables/disables a particular feature.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Gameplay/Feature Set")]
    public class FeatureSet : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// Events that are called when a feature is enabled or disabled.
        /// </summary>
        [Serializable]
        public class FeatureEvents
        {
            [Tooltip("Invoked with the enabled value.")]
            public UnityEvent<bool> onEnabledValue;
            [Tooltip("Invoked when the feature is enabled.")]
            public UnityEvent onEnabled;
            [Tooltip("Invoked when the feature is disabled.")]
            public UnityEvent onDisabled;
        }

        /// <summary>
        /// Defines a feature type.
        /// </summary>
        [Serializable]
        public class Feature
        {
            [Tooltip("The name of the feature, this should be unique among all features in the list.")]
            public string name;
            [Tooltip("Optional: Used for backwards compatibility in case the feature name changes.")]
            public string[] aliases;
            [Tooltip("Whether the feature is enabled or, if false, disabled.")]
            [SerializeField] private bool enabled;
            [Tooltip("Whether to set the feature to enabled on startup.")]
            public bool setOnStart;
            [Tooltip("Event callbacks to expose the feature state to Unity front-end.")]
            public FeatureEvents events;

            private bool _initialSet;
            private bool _wasEnabled;

            /// <summary>
            /// Gets or sets a value indicating whether this feature is enabled or disabled.
            /// </summary>
            public bool Enabled {
                get => enabled;
                set {
                    var wasEnabled = enabled;
                    enabled = value;

                    if (events == null)
                        return;

                    if (wasEnabled == value && _initialSet)
                        return;

                    _initialSet = true;

                    if (enabled) events.onEnabled?.Invoke();
                    else events.onDisabled?.Invoke();
                    events.onEnabledValue?.Invoke(enabled);
                }
            }

            /// <summary>
            /// A function to notify the feature that the <see cref="FeatureSet"/> has started.
            /// </summary>
            public void Start()
            {
                if (setOnStart) SetFeatureEnabled(name, enabled, aliases);
                Enabled = GetOrCreateSourceFeatureSet() ? GetOrCreateSourceFeatureSet().GetFeature(name, enabled, aliases)?.Enabled == true : enabled;
            }

            /// <summary>
            /// A function to notify the feature that it should validate it's enabled state.
            /// </summary>
            public void OnValidate()
            {
                if (!Application.isPlaying) return;
                if (_wasEnabled != enabled || !_initialSet)
                {
                    SetFeatureEnabled(name, enabled, aliases);
                    _wasEnabled = enabled;
                }
            }
            
            public bool IsNameOrAlias(string nameOrAlias)
            {
                if (name == nameOrAlias) return true;
                return aliases is not null && aliases.Any(x => x == nameOrAlias);
            }
        }

        [Tooltip("The features to enable/disable and/or handle events for.")]
        [SerializeField] private List<Feature> features = new();

        private bool _startCalled;
        private static FeatureSet _sourceFeatureSet;
        private Dictionary<string, Feature> _featureCache;

        /// <summary>
        /// Whether this <see cref="FeatureSet"/> is the source feature set containing the current feature state.
        /// </summary>
        private bool IsSourceFeatureSet => _sourceFeatureSet == this;

        private void Start()
        {
            if (features == null) return;
            foreach (var feature in features.ToArray())
                feature.Start();
            _startCalled = true;
        }

        private void OnValidate()
        {
            if (!_startCalled) return;
            if (features == null) return;
            foreach (var feature in features.ToArray())
                feature.OnValidate();
        }

        private void OnEnable()
        {
            if (IsSourceFeatureSet)
                return;

            if (!_startCalled)
                return;

            foreach (var sourceFeature in GetOrCreateSourceFeatureSet().features)
            {
                var localFeature = features.FirstOrDefault(x => x.name == sourceFeature.name);
                if (localFeature != null)
                    localFeature.Enabled = sourceFeature.Enabled;
            }
        }

        /// <summary>
        /// Enable a particular feature.
        /// </summary>
        /// <param name="featureName">The name of the feature to enable.</param>
        public void EnableFeature(string featureName) => SetFeatureEnabled(featureName, true);

        /// <summary>
        /// Disable a particular feature.
        /// </summary>
        /// <param name="featureName">The name of the feature to disable.</param>
        public void DisableFeature(string featureName) => SetFeatureEnabled(featureName, false);

        /// <summary>
        /// Sets a feature enabled/disabled.
        /// </summary>
        /// <param name="name">The name of the feature to enable/disable.</param>
        /// <param name="value">The feature's enabled state.</param>
        /// <param name="aliases">Optional: Used for backwards compatibility in case the feature name changes.</param>
        public static void SetFeatureEnabled(string name, bool value, params string[] aliases)
        {
            if (GetOrCreateSourceFeatureSet())
                GetOrCreateSourceFeatureSet().SetEnabled(name, value, aliases);
        }

        /// <summary>
        /// Gets whether a feature is enabled/disabled.
        /// </summary>
        /// <param name="name">The name of the feature to check.</param>
        /// <param name="aliases">Optional: Used for backwards compatibility in case the feature name changes.</param>
        /// <returns>Whether the feature is enabled.</returns>
        public static bool IsFeatureEnabled(string name, params string[] aliases)
        {
            if (GetOrCreateSourceFeatureSet())
                return GetOrCreateSourceFeatureSet().GetFeature(name, false, aliases)?.Enabled == true;
            return false;
        }

        private void SetEnabled(string featureName, bool featureEnabled, params string[] aliases)
        {
            if (!enabled)
                return;

            var feature = GetFeature(featureName, featureEnabled, aliases);
            if (feature != null)
                feature.Enabled = featureEnabled;

            if (IsSourceFeatureSet)
            {
                var featureSets = MVUtils.FindObjectsOfTypeNonPrefabPooled<FeatureSet>(true).Where(x => x != this);
                foreach (var set in featureSets)
                {
                    if (set.IsSourceFeatureSet) continue;
                    set.SetEnabled(featureName, featureEnabled, aliases);
                }
            }
        }

        private static FeatureSet GetOrCreateSourceFeatureSet()
        {
            if (!Application.isPlaying)
                return null;

            if (_sourceFeatureSet == null)
            {
                var go = new GameObject("[Active Feature Sets]");
                go.SetActive(false);
                _sourceFeatureSet = go.AddComponent<FeatureSet>();
                _sourceFeatureSet.hideFlags = Application.isPlaying ? (HideFlags.HideInHierarchy | HideFlags.NotEditable) : HideFlags.HideAndDontSave;
                go.SetActive(true);
            }

            return _sourceFeatureSet;
        }

        private Feature GetFeature(string featureName, bool defaultEnabled, params string[] aliases)
        {
            if (string.IsNullOrEmpty(featureName))
                return null;
            
            Feature feature = null;
            try
            {
                _featureCache ??= new Dictionary<string, Feature>();
                if (_featureCache.TryGetValue(featureName, out feature))
                    return feature;
            
                if (aliases is not null && aliases.Any(alias => _featureCache.TryGetValue(alias, out feature)))
                    return feature;

                feature = features.FirstOrDefault(x => x.IsNameOrAlias(featureName));
                if (feature is null && IsSourceFeatureSet)
                    features.Add(feature = new Feature
                    {
                        name = featureName,
                        aliases = aliases,
                        Enabled = defaultEnabled
                    });

                _featureCache[featureName] = feature;
                return feature;
            }
            finally
            {
                ApplyAliases();
            }

            void ApplyAliases()
            {
                if (feature is null)
                    return;
                if (aliases is null) 
                    return;
                feature.aliases = feature.aliases?.Concat(aliases).Distinct().ToArray() ?? aliases;
                feature.aliases = feature.aliases.Concat(new[] { featureName }).Distinct().ToArray();
                foreach (var alias in feature.aliases)
                    if (!string.IsNullOrEmpty(alias)) 
                        _featureCache[alias] = feature;
            }
        }
    }
}