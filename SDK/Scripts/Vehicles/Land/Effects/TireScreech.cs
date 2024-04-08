﻿using UnityEngine;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.MenuItems.MenuRootPath + "Vehicles/Land/Effects/Vehicles - Tire Screech Audio", 1)]

    // Class for playing tire screech sounds
    public class TireScreech : MonoBehaviour
    {
        AudioSource snd;
        VehicleParent vp;
        Wheel[] wheels;
        float slipThreshold;
        GroundSurface surfaceType;

        void Start() {
            snd = GetComponent<AudioSource>();
            vp = transform.GetTopmostParentComponent<VehicleParent>();
            wheels = new Wheel[vp.wheels.Length];

            // Get wheels and average slip threshold
            for (int i = 0; i < vp.wheels.Length; i++) {
                wheels[i] = vp.wheels[i];
                if (vp.wheels[i].GetComponent<TireMarkCreate>()) {
                    float newThreshold = vp.wheels[i].GetComponent<TireMarkCreate>().slipThreshold;
                    slipThreshold = i == 0 ? newThreshold : (slipThreshold + newThreshold) * 0.5f;
                }
            }
        }

        void Update() {
            float screechAmount = 0;
            bool allPopped = true;
            bool nonePopped = true;
            float alwaysScrape = 0;

            for (int i = 0; i < vp.wheels.Length; i++) {
                if (wheels[i].connected) {
                    if (Mathf.Abs(F.MaxAbs(wheels[i].sidewaysSlip, wheels[i].forwardSlip, alwaysScrape)) - slipThreshold > 0) {
                        if (wheels[i].popped) {
                            nonePopped = false;
                        }
                        else {
                            allPopped = false;
                        }
                    }

                    if (wheels[i].grounded) {
                        if (wheels[i].contactPoint.surfaceType < GroundSurfaceMaster.Instance.surfaceTypes.Length)
                        {
                            surfaceType = GroundSurfaceMaster.Instance.surfaceTypes[wheels[i].contactPoint.surfaceType];

                            if (surfaceType.alwaysScrape) {
                                alwaysScrape = slipThreshold + Mathf.Min(0.5f, Mathf.Abs(wheels[i].rawRPM * 0.001f));
                            }
                        }
                        else
                        {
                            surfaceType = null;
                        }
                    }

                    screechAmount = Mathf.Max(screechAmount, Mathf.Pow(Mathf.Clamp01(Mathf.Abs(F.MaxAbs(wheels[i].sidewaysSlip, wheels[i].forwardSlip, alwaysScrape)) - slipThreshold), 2));
                }
            }

            // Set audio clip based on number of wheels popped
            if (surfaceType != null && snd) {
                snd.clip = allPopped ? surfaceType.rimSnd : (nonePopped ? surfaceType.tireSnd : surfaceType.tireRimSnd);
            }

            // Set sound volume and pitch
            if (screechAmount > 0) {
                if (!snd.isPlaying) {
                    snd.Play();
                    snd.volume = 0;
                }
                else {
                    snd.volume = Mathf.Lerp(snd.volume, screechAmount * ((vp.groundedWheels * 1.0f) / (wheels.Length * 1.0f)), 2 * Time.deltaTime);
                    snd.pitch = Mathf.Lerp(snd.pitch, 0.5f + screechAmount * 0.9f, 2 * Time.deltaTime);
                }
            }
            else if (snd.isPlaying) {
                snd.volume = 0;
                snd.Stop();
            }
        }
    }
}
