using System;
using UnityEditor;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class BuildTargetScope : IDisposable
    {
        private bool _rolledBack;

        public BuildTargetScope(bool autoRollbackOnDispose = true)
        {
            OriginalBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            OriginalBuildGroup = BuildPipeline.GetBuildTargetGroup(OriginalBuildTarget);
            AutoRollback = autoRollbackOnDispose;
        }

        public BuildTarget OriginalBuildTarget { get; }
        public BuildTargetGroup OriginalBuildGroup { get; }
        public bool AutoRollback { get; set; }

        public void Dispose()
        {
            if (AutoRollback)
            {
                Rollback();
            }
        }

        public void Rollback()
        {
            if (_rolledBack)
                return;

            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(OriginalBuildGroup, OriginalBuildTarget);

            _rolledBack = true;
        }
    }
}
