using System;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    /// <summary>
    /// A generic interface for landplot object properties. Land plot
    /// object properties can allow you to add custom behavior to
    /// buildable objects that users can configure from the build mode.
    /// </summary>
    public interface ILandPlotObjectProperty
    {
        /// <summary>
        /// Whether this property should be hidden.
        /// </summary>
        bool Hidden { get; }
        /// <summary>
        /// The property's unique identifier, used for lookups. Multiple
        /// properties with the same key could cause issues.
        /// </summary>
        string Key { get; }
        /// <summary>
        /// The property's display name. This will be displayed
        /// when the user is in build mode.
        /// </summary>
        string DisplayName { get; }
        /// <summary>
        /// A description for the property. Useful to provide users
        /// with information about what the property does.
        /// </summary>
        string Description { get; }
        /// <summary>
        /// The property's raw value.
        /// </summary>
        object RawValue { get; }

        /// <summary>
        /// Invoked when the property value changes.
        /// </summary>
        event Action<object> ValueChanged;
        /// <summary>
        /// Invoked when the property value is attempted
        /// to be <see cref="Set(object)"/> but fails.
        /// </summary>
        event Action<string> ValidationFailed;

        /// <summary>
        /// Set the property value.
        /// </summary>
        /// <param name="obj">The value to use.</param>
        void Set(object obj);
    }
}
