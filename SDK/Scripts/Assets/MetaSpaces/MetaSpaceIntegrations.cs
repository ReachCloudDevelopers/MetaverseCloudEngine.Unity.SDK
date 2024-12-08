using System;
using MetaverseCloudEngine.Unity.Attributes;
using Unity.VisualScripting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    [Serializable]
    public class MetaSpaceIntegrationOptions
    {
#if PLAYMAKER
        [Header("PlayMaker")]
        public PlayMakerGlobals playMakerGlobals;
#endif

        [Header("Visual Scripting")]
        public VariablesAsset applicationVariables;

        [Header("Oculus Plugin")]
        [Tooltip("Only relevant on the Oculus platform. If true, the OVRManager prefab will be instantiated on start (assuming " +
                 "one is not already present in the scene).")]
        public bool createOvrManagerOnStart = true;

        [Header("Conv AI")]
        [ProtectedField]
        public string convAiApiKey = "";

        public void Validate()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (Application.isPlaying)
                return;
#endif

#if PLAYMAKER
            if (!playMakerGlobals)
            {
                const string kPlayMakerGlobalsResourcePath = "PlayMakerGlobals";
                playMakerGlobals = Resources.Load<PlayMakerGlobals>(kPlayMakerGlobalsResourcePath);
            }
#endif

            if (!applicationVariables)
                applicationVariables = Resources.Load<VariablesAsset>(ApplicationVariables.assetPath);
        }
    }
}