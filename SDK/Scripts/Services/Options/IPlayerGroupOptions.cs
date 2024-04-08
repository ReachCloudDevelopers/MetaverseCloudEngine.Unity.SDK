using MetaverseCloudEngine.Unity.Assets.MetaSpaces;

namespace MetaverseCloudEngine.Unity.Services.Options
{
    public interface IPlayerGroupOptions
    {
        bool AutoSelectPlayerGroup { get; }
        PlayerGroupSelectionMode PlayerGroupSelectionMode { get; }
        PlayerGroup[] PlayerGroups { get; }
    }
}