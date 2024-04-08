namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    /// <summary>
    /// A type of <see cref="LandPlotObjectPropertyBase{T}"/> that is of type <see cref="bool"/>.
    /// </summary>
    public class LandPlotBoolObjectProperty : LandPlotObjectPropertyBase<bool>
    {
        protected override bool IsChanged(bool oldValue, bool newValue)
        {
            return oldValue != newValue;
        }
    }
}
