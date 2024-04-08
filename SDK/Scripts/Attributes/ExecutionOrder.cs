namespace MetaverseCloudEngine.Unity.Attributes
{
    /// <summary>
    /// A class which contains execution order constants.
    /// </summary>
    public static class ExecutionOrder
    {
        /// <summary>
        /// The execution order for the pre-initialization phase.
        /// </summary>
        public const int PreInitialization = -int.MaxValue + 1;
        /// <summary>
        /// The execution order for the initialization phase.
        /// </summary>
        public const int Initialization = PreInitialization + 1;
        /// <summary>
        /// The execution order for the post-initialization phase.
        /// </summary>
        public const int PostInitialization = Initialization + 1;
        /// <summary>
        /// The execution order for the finalization phase.
        /// </summary>
        public const int Finalize = PostInitialization + 2;
    }
}
