#if MV_XR_MANAGEMENT
using System.Collections;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
#if MV_XR_HANDS && MV_OPENXR
using UnityEngine.XR.Hands.OpenXR;
#endif
using UnityEngine.XR.Management;

namespace MetaverseCloudEngine.Unity.XR
{
    /// <summary>
    /// Manages the lifecycle of the XR subsystem and adds
    /// the ability to activate and deactivate it.
    /// </summary>
    [AddComponentMenu("")]
    public class XRSubsystemAPI : MonoBehaviour
    {
        private static XRSubsystemAPI _instance;
        private static bool _handTrackingRestarting;

        /// <summary>
        /// Should the app attempt to start in XR mode?
        /// </summary>
        public static bool ShouldAutoStartInXR {
            get => PlayerPrefs.GetInt(nameof(ShouldAutoStartInXR), 0) == 1;
            set {
                PlayerPrefs.SetInt(nameof(ShouldAutoStartInXR), value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            var inst = new GameObject(nameof(XRSubsystemAPI));
            _instance = inst.AddComponent<XRSubsystemAPI>();
            DontDestroyOnLoad(inst);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void RestartHandTracking()
        {
            if (_instance)
                _instance.StartCoroutine(RestartHandTrackingCoroutine());
        }

#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR
        private void Start()
        {
            if (ShouldAutoStartInXR)
                StartXR();
        }

        private void Update()
        {
            var keyboard = InputSystem.GetDevice<Keyboard>();
            if (keyboard == null)
                return;

            if (keyboard.f3Key.wasPressedThisFrame)
            {
                if (!XRSettings.enabled)
                {
                    StartXR();
                    ShouldAutoStartInXR = true;
                }
                else
                    XRInputTrackingAPI.CenterOrigin();
            }

            if (keyboard.f4Key.wasPressedThisFrame)
            {
                StopXR();
                ShouldAutoStartInXR = false;
            }
        }

        private void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting && !Application.isEditor) return;
            StopXR();
        }
#endif

        /// <summary>
        /// Starts the XR subsystem.
        /// </summary>
        public static void StartXR()
        {
            if (!_instance) return;
            _instance.StartCoroutine(StartXRCoroutine());
            _instance.StartCoroutine(RestartHandTrackingCoroutine());
        }

        /// <summary>
        /// Stops the XR subsystem.
        /// </summary>
        public static void StopXR()
        {
            if (MetaverseProgram.IsQuitting)
                return;

            if (_instance)
                _instance.StopAllCoroutines();

            if (!XRGeneralSettings.Instance || !XRGeneralSettings.Instance.Manager)
                return;

            if (!XRGeneralSettings.Instance.Manager.activeLoader) // No loader is active, nothing to stop
                return;
            
            XRGeneralSettings.Instance.Manager.StopSubsystems();
            XRGeneralSettings.Instance.Manager.DeinitializeLoader();
            
            Cursor.lockState = CursorLockMode.None;
            InputSystem.FlushDisconnectedDevices();
            
            MetaverseDispatcher.WaitForSeconds(0.1f, () =>
            {
                if (MetaverseProgram.IsQuitting) return;
                QualitySettings.SetQualityLevel(QualitySettings.GetQualityLevel(), true);
                Screen.SetResolution(Screen.width, Screen.height, Screen.fullScreen);
            });
        }

        public static void RestartXR()
        {
            if (_instance)
                _instance.StartCoroutine(RestartXRCoroutine());
            else
                MetaverseProgram.Logger.LogError("[XRSubsystemAPI] Instance is null, cannot restart XR.");
        }

        private static IEnumerator RestartXRCoroutine()
        {
            if (_instance)
            {
                _instance.StopAllCoroutines();
                XRGeneralSettings.Instance.Manager.StopSubsystems();
                XRGeneralSettings.Instance.Manager.DeinitializeLoader();
                yield return null;
                yield return StartXRCoroutine();
            }
            else
            {
                MetaverseProgram.Logger.LogError("[XRSubsystemAPI] Instance is null, cannot restart XR.");
            }
        }
        
        private static IEnumerator RestartHandTrackingCoroutine()
        {
#if MV_XR_HANDS && MV_OPENXR
            if (_handTrackingRestarting)
                yield break;
            _handTrackingRestarting = true;
            HandTracking.subsystem?.Stop();
            yield return new WaitUntil(() => HandTracking.subsystem == null || !HandTracking.subsystem.running);
            yield return new WaitForSeconds(1.5f);
            HandTracking.subsystem?.Start();
            _handTrackingRestarting = false;
#else
            yield break;
#endif
        }

        private static IEnumerator StartXRCoroutine()
        {
            if (XRSettings.enabled)
                yield break;

            MetaverseProgram.Logger.Log("Initializing XR");

            yield return XRGeneralSettings.Instance.Manager.InitializeLoader();
            
            if (XRGeneralSettings.Instance.Manager.activeLoader == null)
            {
                MetaverseProgram.Logger.Log("Initializing XR Failed");

#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
                MetaverseProgram.OnInitialized(() =>
                {
                    if (MetaverseProgram.AppUpdateRequired)
                        return;

                    MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(
                        "Start XR Failed",
                        "Failed to start in XR mode. Would you like to retry?",
                        "Retry", "No",
                        StartXR,
                        () =>
                        {
                            ShouldAutoStartInXR = false;
                            StopXR();
                        });
                });
#endif
            }
            else
            {
                MetaverseProgram.Logger.Log("[XRSubsystemActivator] Starting XR...");
                XRGeneralSettings.Instance.Manager.StartSubsystems();
#if MV_XR_HANDS && MV_OPENXR
                HandTracking.subsystem?.Start();
#endif
            }
            
            yield return new WaitForSeconds(0.1f);
            
            XRInputTrackingAPI.CenterOrigin();
        }
        
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RestartHandTracking();
        }
    }
}
#endif