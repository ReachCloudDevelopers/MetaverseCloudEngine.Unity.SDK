/* Copyright (C) 2024 Reach Cloud - All Rights Reserved */
using System.Threading.Tasks;

namespace MetaverseCloudEngine.Unity.Account.Abstract
{
    /// <summary>
    /// Stores the login information for the user.
    /// </summary>
    public interface ILoginStore
    {
        /// <summary>
        /// Initializes the <see cref="ILoginStore"/> system.
        /// </summary>
        /// <returns>An awaitable task.</returns>
        Task InitializeAsync();
    }
}