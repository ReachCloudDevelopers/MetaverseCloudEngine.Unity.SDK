using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using MetaverseCloudEngine.Unity.Labels;
using UnityEngine.Networking;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// This component provides functionality for building a string value
    /// using multiple parts.
    /// </summary>
    [HideMonoScript]
    public class BuildString : TriInspectorMonoBehaviour
    {
        [Tooltip("The string parts that you'd like to use.")]
        public List<Label> parts = new();
        [Tooltip("Should the string be built at the start of the level?")]
        public bool buildOnStart = true;
        [Tooltip("Should the string be built whenever all string parts are not empty?")]
        public bool buildOnPartsFilled;
        [Tooltip("The event that occurs when the string is built successfully.")]
        public UnityEvent<string> onBuilt;

        // - Deprecated -
        [HideInInspector, SerializeField]
        private List<string> stringParts = new();
        // -----

        private void Awake()
        {
            UpgradeOldFields();
        }

        private void OnValidate()
        {
            UpgradeOldFields();
        }

        private void Start()
        {
            if (buildOnStart)
                Build();
        }

        private void UpgradeOldFields()
        {
            if (stringParts is { Count: > 0 })
            {
                parts = stringParts.Select(x => (Label)x).ToList();
                stringParts = null;
            }
        }

        /// <summary>
        /// Build the string.
        /// </summary>
        public void Build()
        {
            UpgradeOldFields();

            Label.GetAllAsync(parts.ToArray(), p =>
            {
                var builtString = string.Join(string.Empty, p);
                onBuilt?.Invoke(builtString);
            });
        }

        public void AddString(LabelReference value) => value.label?.GetValueAsync(v => AddString(v));

        /// <summary>
        /// Add a new string to the <see cref="parts"/> list.
        /// </summary>
        /// <param name="value">The value to add to the list.</param>
        public void AddString(string value)
        {
            parts.Add(value);
            TryBuild();
        }

        public void RemoveString(LabelReference value) => value.label?.GetValueAsync(v => RemoveString(v));

        /// <summary>
        /// Remove a string from the <see cref="parts"/> list.
        /// </summary>
        /// <param name="value">The value to remove from the string parts.</param>
        public void RemoveString(string value)
        {
            parts.Remove(value);
            TryBuild();
        }

        /// <summary>
        /// Remove a string from the <see cref="parts"/> list.
        /// </summary>
        /// <param name="value">The value to remove from the string parts.</param>
        public void RemoveStringUrlEncoded(string value)
        {
            parts.Remove(UnityWebRequest.EscapeURL(value));
            TryBuild();
        }

        public void SetString0(LabelReference value) => value.label?.GetValueAsync(v => SetString0(v));

        public void SetString0(string value)
        {
            if (parts.Count > 0)
                parts[0] = value;
            TryBuild();
        }

        public void SetString0UrlEncoded(string value)
        {
            if (parts.Count > 0)
                parts[0] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString1(LabelReference value) => value.label?.GetValueAsync(v => SetString1(v));

        public void SetString1(string value)
        {
            if (parts.Count > 1)
                parts[1] = value;
            TryBuild();
        }

        public void SetString1UrlEncoded(string value)
        {
            if (parts.Count > 1)
                parts[1] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString2(LabelReference value) => value.label?.GetValueAsync(v => SetString2(v));

        public void SetString2(string value)
        {
            if (parts.Count > 2)
                parts[2] = value;
            TryBuild();
        }

        public void SetString2UrlEncoded(string value)
        {
            if (parts.Count > 2)
                parts[2] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString3(LabelReference value) => value.label?.GetValueAsync(v => SetString3(v));

        public void SetString3(string value)
        {
            if (parts.Count > 3)
                parts[3] = value;
            TryBuild();
        }

        public void SetString3UrlEncoded(string value)
        {
            if (parts.Count > 3)
                parts[3] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString4(LabelReference value) => value.label?.GetValueAsync(v => SetString4(v));

        public void SetString4(string value)
        {
            if (parts.Count > 4)
                parts[4] = value;
            TryBuild();
        }

        public void SetString4UrlEncoded(string value)
        {
            if (parts.Count > 4)
                parts[4] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString5(LabelReference value) => value.label?.GetValueAsync(v => SetString5(v));

        public void SetString5(string value)
        {
            if (parts.Count > 5)
                parts[5] = value;
            TryBuild();
        }

        public void SetString5UrlEncoded(string value)
        {
            if (parts.Count > 5)
                parts[1] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString6(LabelReference value) => value.label?.GetValueAsync(v => SetString6(v));

        public void SetString6(string value)
        {
            if (parts.Count > 6)
                parts[6] = value;
            TryBuild();
        }

        public void SetString6UrlEncoded(string value)
        {
            if (parts.Count > 6)
                parts[1] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString7(LabelReference value) => value.label?.GetValueAsync(v => SetString7(v));

        public void SetString7(string value)
        {
            if (parts.Count > 7)
                parts[7] = value;
            TryBuild();
        }

        public void SetString7UrlEncoded(string value)
        {
            if (parts.Count > 7)
                parts[1] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString8(LabelReference value) => value.label?.GetValueAsync(v => SetString8(v));

        public void SetString8(string value)
        {
            if (parts.Count > 8)
                parts[8] = value;
            TryBuild();
        }

        public void SetString8UrlEncoded(string value)
        {
            if (parts.Count > 8)
                parts[8] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString9(LabelReference value) => value.label?.GetValueAsync(v => SetString9(v));

        public void SetString9(string value)
        {
            if (parts.Count > 9)
                parts[9] = value;
            TryBuild();
        }

        public void SetString9UrlEncoded(string value)
        {
            if (parts.Count > 9)
                parts[9] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        public void SetString10(LabelReference value) => value.label?.GetValueAsync(v => SetString10(v));

        public void SetString10(string value)
        {
            if (parts.Count > 10)
                parts[10] = value;
            TryBuild();
        }

        public void SetString10UrlEncoded(string value)
        {
            if (parts.Count > 10)
                parts[10] = UnityWebRequest.EscapeURL(value);
            TryBuild();
        }

        private void TryBuild()
        {
            Label.GetAllAsync(parts.ToArray(), parts =>
            {
                if (!buildOnPartsFilled || parts.All(x => !string.IsNullOrEmpty(x)))
                    Build();
            });
        }
    }
}
