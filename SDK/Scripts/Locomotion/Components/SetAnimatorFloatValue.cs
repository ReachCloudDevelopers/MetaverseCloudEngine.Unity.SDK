using UnityEngine;

namespace MetaverseCloudEngine.Unity.Locomotion.Components
{
    public class SetAnimatorFloatValue : SetAnimatorValueBase<float>
    {
        protected override void SetInternal(float value)
        {
            Animator.SetFloat(ParameterHash, value);
        }
    }
}