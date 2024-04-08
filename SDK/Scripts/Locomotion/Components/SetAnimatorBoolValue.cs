using UnityEngine;

namespace MetaverseCloudEngine.Unity.Locomotion.Components
{
    public class SetAnimatorBoolValue : SetAnimatorValueBase<bool>
    {
        protected override void SetInternal(bool value)
        {
            Animator.SetBool(ParameterHash, value);
        }
    }
}