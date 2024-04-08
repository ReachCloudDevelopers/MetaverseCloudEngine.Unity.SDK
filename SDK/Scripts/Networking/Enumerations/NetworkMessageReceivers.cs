namespace MetaverseCloudEngine.Unity.Networking.Enumerations
{
    /// <summary>
    /// The receivers of a network message.
    /// </summary>
    public enum NetworkMessageReceivers
    {
        /// <summary>
        /// All clients.
        /// </summary>
        All = 0,
        /// <summary>
        /// All clients except this one.
        /// </summary>
        Others = 1,
        /// <summary>
        /// The server / host.
        /// </summary>
        Host = 2
    }
}