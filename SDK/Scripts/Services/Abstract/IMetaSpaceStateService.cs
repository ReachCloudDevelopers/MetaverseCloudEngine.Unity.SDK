using System;

namespace MetaverseCloudEngine.Unity.Services.Abstract
{
    public interface IMetaSpaceStateService : IMetaSpaceService
    {
        event Action MetaSpaceStarted;
        event Action MetaSpaceEnded;

        bool IsStarted { get; }
        bool CanStartGame { get; }

        bool TryStartGame();
        bool TryEndGame();
    }
}