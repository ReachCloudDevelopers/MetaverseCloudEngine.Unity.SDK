using System;
using MetaverseCloudEngine.Unity.Avatar.Components;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Abstract
{
    public delegate void VrIkSystemCalibratedDelegate(string calibrationDataJson);

    public interface IVrIkSystem
    {
        event VrIkSystemCalibratedDelegate Calibrated;
        bool HasCalibrationData { get; }
        VrTrackers Trackers { get; }
        Animator Avatar { get; }
        Transform RootTransform { get; set; }

        void UpdateTrackers(VrTrackers vrt);
        void UpdateAvatar(Animator avatar);
        void Calibrate(VrIkSystemCalibratedDelegate onCalibrated = null, Action onFailed = null);
        void Calibrate(string calibrationJson, Action onCalibrated = null, Action onFailed = null);
        void SetIKActive(bool active);
        void Destroy();
    }
}
