// Intentionally left as a no-op.
// We previously injected a custom component titlebar for Metaverse .NET scripts.
// Unity's built-in component header now handles dropdown and expand/collapse behavior.

#if MV_DOTNOW_SCRIPTING
namespace MetaverseCloudEngine.Unity.Editors
{
    internal static class MetaverseDotNetScriptTitleBarEditor
    {
    }
}
#endif // MV_DOTNOW_SCRIPTING
