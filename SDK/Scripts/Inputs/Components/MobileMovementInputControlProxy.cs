using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public sealed class MobileMovementInputControlProxy : OnScreenControl
    {
        public void SendValueToControlPublic(Vector2 value)
        {
            if (!isActiveAndEnabled)
                return;
            SendValueToControl(value);
        }

        [InputControl(layout = "Vector2")]
        [SerializeField]
        private string path;

        protected override string controlPathInternal
        {
            get => path;
            set => path = value;
        }
    }
}