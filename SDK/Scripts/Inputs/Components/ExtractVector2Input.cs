using UnityEngine;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public class ExtractVector2Input : ExtractInput<Vector2>
    {
        protected override string ExpectedControlType() => "Vector2";
    }
}