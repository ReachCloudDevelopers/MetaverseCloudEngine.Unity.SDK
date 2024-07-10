using UnityEngine;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public class ExtractVector3Input : ExtractInput<Vector3>
    {
        protected override string ExpectedControlType() => "Vector3";
    }
}