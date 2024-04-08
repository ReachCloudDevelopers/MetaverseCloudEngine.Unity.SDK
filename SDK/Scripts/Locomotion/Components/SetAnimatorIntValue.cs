using UnityEngine;

namespace MetaverseCloudEngine.Unity.Locomotion.Components
{
    public class SetAnimatorIntValue : SetAnimatorValueBase<int>
    {
        protected override void SetInternal(int value)
        {
            Animator.SetInteger(ParameterHash, value);
        }
    }
}