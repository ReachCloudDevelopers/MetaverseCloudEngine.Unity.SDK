using UnityEngine;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    public static class TimeValues
    {
        public static float FixedTimeFactor => 0.01f / Time.fixedDeltaTime;
        public static float InverseFixedTimeFactor => 1.0f / FixedTimeFactor;
    }
}