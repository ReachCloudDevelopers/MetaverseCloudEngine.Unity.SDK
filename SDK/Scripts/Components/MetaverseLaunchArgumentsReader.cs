using System;
using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// This class is used to read the launch arguments from the Metaverse Cloud Engine program.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Program/Metaverse Launch Arguments Reader")]
    public class MetaverseLaunchArgumentsReader : MetaSpaceBehaviour
    {
        /// <summary>
        /// Defines a specific launch property to detect from the URL.
        /// </summary>
        [Serializable]
        [DeclareFoldoutGroup("foldout", Title = "$" + nameof(key))]
        public class CustomLaunchDataProperty
        {
            [Group("foldout")]
            [Space]
            [Tooltip("The launch property key to look up.")]
            public string key;

            [Title("Success / Fail")]
            [Group("foldout")]
            [Tooltip("Invoked when the property is found.")]
            public UnityEvent onFound;
            [Group("foldout")]
            [Tooltip("Invoked when the property is not found.")]
            public UnityEvent onNotFound;

            [Title("Values")]
            [Group("foldout")]
            [Tooltip("Invoked when the you get the string value from the custom data.")]
            public UnityEvent<string> onString;
            [Group("foldout")]
            [Tooltip("Invoked when the you get the float value from the custom data.")]
            public UnityEvent<float> onFloat;
            [Group("foldout")]
            [Tooltip("Invoked when the you get the int value from the custom data.")]
            public UnityEvent<int> onInt;
            [Group("foldout")]
            [Tooltip("Invoked when the you get the bool value from the custom data.")]
            public UnityEvent<bool> onBool;

            /// <summary>
            /// Reads the custom data.
            /// </summary>
            /// <param name="data">The custom data.</param>
            public void Read(IDictionary<string, string> data)
            {
                if (data.TryGetValue(key, out var value))
                {
                    if (int.TryParse(value, out var i))
                    {
                        onInt?.Invoke(i);
                        onFloat?.Invoke(i);
                    }
                    else if (float.TryParse(value, out var f)) onFloat?.Invoke(f);
                    else if (bool.TryParse(value, out var b)) onBool?.Invoke(b);
                    if (value is not null)
                    {
                        onString?.Invoke(value.ToString());
                        onFound?.Invoke();
                    }
                    else
                    {
                        onNotFound?.Invoke();
                    }
                }
                else
                {
                    onNotFound?.Invoke();
                }
            }
        }

        [Tooltip("The launch properties to detect.")]
        [SerializeField] private CustomLaunchDataProperty[] arguments = Array.Empty<CustomLaunchDataProperty>();

        protected override void OnMetaSpaceBehaviourInitialize()
        {
            base.OnMetaSpaceBehaviourInitialize();

            if (MetaverseProgram.LaunchArguments != null)
            {
                foreach (var property in arguments)
                {
                    property?.Read(MetaverseProgram.LaunchArguments);
                }
            }
        }
    }
}
