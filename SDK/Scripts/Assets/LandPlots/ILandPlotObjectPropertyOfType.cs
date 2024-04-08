namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    /// <summary>
    /// A type of <see cref="ILandPlotObjectProperty"/> with a generic type
    /// for custom behavior to be easier to implement.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    public interface ILandPlotObjectPropertyOfType<T> : ILandPlotObjectProperty
    {
        /// <summary>
        /// Gets or sets the properties typed value.
        /// </summary>
        T Value { get; set; }
    }
}
