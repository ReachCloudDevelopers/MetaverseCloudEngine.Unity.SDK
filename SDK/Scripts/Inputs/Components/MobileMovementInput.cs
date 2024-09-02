using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    /// <summary>
    /// This component translates touch input into movement input for the player.
    /// Left side of the screen is for movement, right side is for looking around.
    /// It utilizes the <see cref="Input.touches"/> API to get the touch positions,
    /// and calculates the drag delta to move the player.
    /// </summary>
    public class MobileMovementInput : MonoBehaviour
    {
        [Required]
        [SerializeField] private MobileMovementInputControlProxy movementControl;
        [SerializeField] private float movementSensitivity = 1.0f;
        [Required]
        [SerializeField] private MobileMovementInputControlProxy lookControl;
        [SerializeField] private float lookSensitivity = 1.0f;
        
        private Vector2 _movementTouchStart;
        private Vector2 _lookTouchStart;
        
        private int _movementTouch = -1;
        private int _lookTouch = -1;

        private void Awake()
        {
            if (!UnityEngine.Device.Application.isMobilePlatform && !Application.isEditor)
                enabled = false;
        }

        private void Update()
        {
            if (Input.touchCount <= 0)
                return;
            
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                switch (touch.phase)
                {
                    case TouchPhase.Began when IgnoreInput():
                        return;
                    case TouchPhase.Ended:
                    {
                        if (touch.fingerId == _movementTouch)
                        {
                            movementControl.SendValueToControlPublic(Vector2.zero);
                            _movementTouch = -1;
                        }
                        else if (touch.fingerId == _lookTouch)
                        {
                            lookControl.SendValueToControlPublic(Vector2.zero);
                            _lookTouch = -1;
                        }
                        continue;
                    }
                    default:
                        switch (touch.phase)
                        {
                            case TouchPhase.Began when touch.position.x < Screen.width / 2f:
                                _movementTouchStart = touch.position;
                                _movementTouch = touch.fingerId;
                                break;
                            case TouchPhase.Began when touch.position.x >= Screen.width / 2f:
                                _lookTouchStart = touch.position;
                                _lookTouch = touch.fingerId;
                                break;
                            case TouchPhase.Moved:
                            {
                                if (touch.fingerId == _lookTouch)
                                {
                                    var delta = touch.position - _lookTouchStart;
                                    lookControl.SendValueToControlPublic(delta * lookSensitivity);
                                    _lookTouchStart = touch.position;   
                                }
                                else if (touch.fingerId == _movementTouch)
                                {
                                    var delta = touch.position - _movementTouchStart;
                                    movementControl.SendValueToControlPublic(delta * movementSensitivity);
                                }
                                break;
                            }
                        }

                        break;
                }
            }
        }

        private static bool IgnoreInput()
        {
            // If the game is not focused, the user is tapping a UI element, or they are typing in an input field, ignore input
            if (!UnityEngine.Device.Application.isFocused)
                return true;

            if (MVUtils.IsPointerOverUI())
                return true;

            if (MVUtils.IsUnityInputFieldFocused())
                return true;
            
            if (InputButtonEvent.WebViewCheckInputFieldFocusedCallback?.Invoke() ?? false)
                return true;
            
            return false;
        }
    }
}