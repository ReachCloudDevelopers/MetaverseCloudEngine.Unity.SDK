using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using MetaverseCloudEngine.Unity.Scripting.Components;

namespace MetaverseCloudEngine.Unity.Editors
{
    [InitializeOnLoad]
    internal static class KeepMetaverseScriptExpanded
    {
        private static double _nextCheckTime;
        private const double CheckInterval = 0.33; // seconds

        static KeepMetaverseScriptExpanded()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextCheckTime)
                return;
            _nextCheckTime = now + CheckInterval;

            var tracker = ActiveEditorTracker.sharedTracker;
            if (tracker == null)
                return;
            var editors = tracker.activeEditors;
            if (editors == null || editors.Length == 0)
                return;

            for (int i = 0; i < editors.Length; i++)
            {
                var ed = editors[i];
                if (ed == null)
                    continue;

                // Single target
                if (ed.target is MetaverseScript s)
                {
                    if (!InternalEditorUtility.GetIsInspectorExpanded(s))
                    {
                        InternalEditorUtility.SetIsInspectorExpanded(s, true);
                        InternalEditorUtility.RepaintAllViews();
                        ActiveEditorTracker.sharedTracker.ForceRebuild();
                        if (ed is MetaverseScriptEditor editor)
                        {
                            editor.IsCollapsed = !editor.IsCollapsed;
                        }
                        else Debug.LogWarning($"Editor {ed.name} is not a MetaverseScriptEditor");
                    }
                }

                // Multi-object selection
                var targets = ed.targets;
                if (targets == null || targets.Length <= 1)
                    continue;
                foreach (var t in targets)
                {
                    if (t is MetaverseScript ms && !InternalEditorUtility.GetIsInspectorExpanded(ms))
                        InternalEditorUtility.SetIsInspectorExpanded(ms, true);
                }
            }
        }
    }
}
