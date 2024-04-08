using MetaverseCloudEngine.Unity.Attributes;
using Newtonsoft.Json;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    [Serializable]
    public class LandPlotOptionsObjectPropertyOption
    {
        [Serializable]
        public class Events
        {
            public UnityEvent<string> onChosen = new();
            public UnityEvent<string> onNotChosen = new();
        }

        [DisallowNull] public string name;
        public Sprite icon;
        public Events events = new();
    }
}
