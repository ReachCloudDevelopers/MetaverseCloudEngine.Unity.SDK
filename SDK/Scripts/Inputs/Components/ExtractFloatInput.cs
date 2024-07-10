namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public class ExtractFloat : ExtractInput<float>
    {
        protected override string ExpectedControlType() => "Axis";
    }
}