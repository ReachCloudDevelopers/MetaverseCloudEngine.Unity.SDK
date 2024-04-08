using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [Serializable]
    public class VrTrackers
    {
        [SerializeField] private Transform hmd;
        [SerializeField] private Transform lHand;
        [SerializeField] private Transform rHand;
        [SerializeField] private Transform hip;
        [SerializeField] private Transform lFoot;
        [SerializeField] private Transform rFoot;

        public event Action TrackersChanged;

        public Transform Hmd {
            get => hmd;
            set {
                if (hmd != value)
                {
                    TrackersChanged?.Invoke();
                    hmd = value;
                }
            }
        }
        public Transform LHand {
            get => lHand; 
            set {
                if (lHand != value)
                {
                    TrackersChanged?.Invoke();
                    lHand = value;
                }
            }
        }
        public Transform RHand {
            get => rHand; 
            set {
                if (rHand != value)
                {
                    TrackersChanged?.Invoke();
                    rHand = value;
                }
            }
        }
        public Transform Hip {
            get => hip; 
            set {
                if (hip != value)
                {
                    TrackersChanged?.Invoke();
                    hip = value;
                }
            }
        }
        public Transform LFoot {
            get => lFoot; 
            set {
                if (lFoot != value)
                {
                    TrackersChanged?.Invoke();
                    lFoot = value;
                }
            }
        }
        public Transform RFoot {
            get => rFoot; 
            set {
                if (rFoot != value)
                {
                    TrackersChanged?.Invoke();
                    rFoot = value;
                }
            }
        }

        public void UpdateAll(
            bool notify,
            Transform hmd = null,
            Transform lHand = null,
            Transform rHand = null,
            Transform hip = null,
            Transform lFoot = null,
            Transform rFoot = null)
        {
            this.hmd = hmd;
            this.rHand = rHand;
            this.lHand = lHand;
            this.hip = hip;
            this.lFoot = lFoot;
            this.rFoot = rFoot;

            if (notify)
                TrackersChanged?.Invoke();
        }
    }
}
