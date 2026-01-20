#if METAVERSE_CLOUD_ENGINE
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.XR;
using MetaverseCloudEngine.Unity.XR.Components;
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
using RootMotion.FinalIK;
#endif
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Integrations.FullBodyEstimation.Integrations.FinalIK // Bad namespace, can't change it though...
{
    [HideMonoScript]
    public class VRIKTrackerWeightsManager : TriInspectorMonoBehaviour
    {
        [ListDrawerSettings(Draggable = true)]
        [SerializeField] private MetaverseXRTracker[] trackers;
        [SerializeField, Min(0)] private int humanIndex;
        [SerializeField] private float interpolationSpeed = 2.5f;
        [SerializeField] private float maxRotationWeight = 0;
        [SerializeField] private float maxPositionWeight = 0;
        [SerializeField] private bool modifyPositionWeights = true;
        [SerializeField] private bool modifyRotationWeights = true;
        [SerializeField] private bool useProceduralLegMovement = true;
        [SerializeField] private bool useRecommendedLocomotionSettings = true;

        private readonly Dictionary<MetaverseXRTrackerType, MetaverseXRTracker> _trackers = new();
        private readonly Dictionary<MetaverseXRTrackerType, float> _confidence = new();
        private bool _isTracking;
        
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
        private VRIK _vrIk;
#endif

        private void OnEnable()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (MetaverseXRTracker.InternalTrackerInterface != null)
                MetaverseXRTracker.InternalTrackerInterface.ConfidenceChanged += OnHumanPoseConfidence;
            MetaverseXRTracker.InternalTrackerInterfaceChanged += OnInternalTrackingInterfaceChanged;
            _vrIk = GetComponent<VRIK>();
#endif
            foreach (var tracker in trackers)
            {
                if (tracker != null)
                    _trackers[tracker.Type] = tracker;
            }
            ZeroOutAllIkWeights();
        }

#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
        private void OnInternalTrackingInterfaceChanged(XR.Abstract.IMetaverseXRTrackerManager prev, XR.Abstract.IMetaverseXRTrackerManager current)
        {
            if (!this) return;
            if (prev != null) prev.ConfidenceChanged -= OnHumanPoseConfidence;
            if (current != null) current.ConfidenceChanged += OnHumanPoseConfidence;
        }

        private void OnDisable()
        {
            if (MetaverseXRTracker.InternalTrackerInterface != null)
                MetaverseXRTracker.InternalTrackerInterface.ConfidenceChanged -= OnHumanPoseConfidence;
        }
#endif

        private void ZeroOutAllIkWeights()
        {
            _isTracking = false;
            
    #if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            
            if (!_vrIk)
                return;
            
            if (_trackers.TryGetValue(MetaverseXRTrackerType.Head, out _))
            {
                _vrIk.solver.spine.positionWeight = 0;
                _vrIk.solver.spine.rotationWeight = 0;
            }

            if (_trackers.TryGetValue(MetaverseXRTrackerType.Pelvis, out _))
            {
                _vrIk.solver.spine.positionWeight = 0;
                _vrIk.solver.spine.rotationWeight = 0;
            }
            
            if (_trackers.TryGetValue(MetaverseXRTrackerType.LeftWrist, out _))
            {
                _vrIk.solver.leftArm.positionWeight = 0;
                _vrIk.solver.leftArm.rotationWeight = 0;
            }
            
            if (_trackers.TryGetValue(MetaverseXRTrackerType.RightWrist, out _))
            {
                _vrIk.solver.rightArm.positionWeight = 0;
                _vrIk.solver.rightArm.rotationWeight = 0;
            }
            
            if (_trackers.TryGetValue(MetaverseXRTrackerType.LeftAnkle, out _))
            {
                _vrIk.solver.leftLeg.positionWeight = 0;
                _vrIk.solver.leftLeg.rotationWeight = 0;
            }
            
            if (_trackers.TryGetValue(MetaverseXRTrackerType.RightAnkle, out _))
            {
                _vrIk.solver.rightLeg.positionWeight = 0;
                _vrIk.solver.rightLeg.rotationWeight = 0;
            }

#endif
            _confidence.Clear();
        }

        private void Update()
        {
            if (!_isTracking)
                return;

#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (_vrIk != null)
            {
                float targetLocomotion = useProceduralLegMovement ? 1f : 0f;
                _vrIk.solver.locomotion.weight = Mathf.Lerp(_vrIk.solver.locomotion.weight, targetLocomotion, interpolationSpeed * Time.deltaTime);

                if (useRecommendedLocomotionSettings)
                {
                    _vrIk.solver.locomotion.footDistance = 0.2f;
                    _vrIk.solver.locomotion.stepThreshold = 0.2f;
                    _vrIk.solver.locomotion.angleThreshold = 45f;
                }
            }
#endif
            
            if (_confidence.TryGetValue(MetaverseXRTrackerType.Head, out var headConfidence))
                OnHeadConfidence(headConfidence);
            if (_confidence.TryGetValue(MetaverseXRTrackerType.Pelvis, out var hipConfidence))
                OnHipConfidence(hipConfidence);
            if (_confidence.TryGetValue(MetaverseXRTrackerType.LeftWrist, out var leftArmConfidence))
                OnLeftArmConfidence(leftArmConfidence);
            if (_confidence.TryGetValue(MetaverseXRTrackerType.RightWrist, out var rightArmConfidence))
                OnRightArmConfidence(rightArmConfidence);
            if (_confidence.TryGetValue(MetaverseXRTrackerType.LeftAnkle, out var leftLegConfidence))
                OnLeftLegConfidence(leftLegConfidence);
            if (_confidence.TryGetValue(MetaverseXRTrackerType.RightAnkle, out var rightLegConfidence))
                OnRightLegConfidence(rightLegConfidence);
        }

        private void OnHumanPoseConfidence(int index, MetaverseXRTrackerType metaverseXRTrackerType, float f)
        {
            if (humanIndex != index)
                return;
            
            if (metaverseXRTrackerType == MetaverseXRTrackerType.None)
                return;

            if (!_trackers.TryGetValue(metaverseXRTrackerType, out var tracker) || !tracker.isActiveAndEnabled)
            {
                if (_confidence.TryGetValue(metaverseXRTrackerType, out _))
                {
                    _confidence.Remove(metaverseXRTrackerType);
                    if (_confidence.Count == 0)
                        _isTracking = false;
                }

                return;
            }
            
            _confidence[metaverseXRTrackerType] = f;
            _isTracking = true;
        }

        private float GetTargetWeight(float maxWeight, float confidence)
        {
            if (maxWeight <= 0) return 1f;
            return Mathf.InverseLerp(0, maxWeight, confidence);
        }

        private void OnRightLegConfidence(float f)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (modifyPositionWeights)
                _vrIk.solver.rightLeg.positionWeight = Mathf.Lerp(_vrIk.solver.rightLeg.positionWeight, useProceduralLegMovement ? 0 : GetTargetWeight(maxPositionWeight, f), interpolationSpeed * Time.deltaTime);
            if (modifyRotationWeights)
                _vrIk.solver.rightLeg.rotationWeight = Mathf.Lerp(_vrIk.solver.rightLeg.rotationWeight, useProceduralLegMovement ? 0 : GetTargetWeight(maxRotationWeight, f), interpolationSpeed * Time.deltaTime);
#endif
        }

        private void OnLeftLegConfidence(float f)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (modifyPositionWeights)
                _vrIk.solver.leftLeg.positionWeight = Mathf.Lerp(_vrIk.solver.leftLeg.positionWeight, useProceduralLegMovement ? 0 : GetTargetWeight(maxPositionWeight, f), interpolationSpeed * Time.deltaTime);
            if (modifyRotationWeights)
                _vrIk.solver.leftLeg.rotationWeight = Mathf.Lerp(_vrIk.solver.leftLeg.rotationWeight, useProceduralLegMovement ? 0 : GetTargetWeight(maxRotationWeight, f), interpolationSpeed * Time.deltaTime);
#endif
        }

        private void OnRightArmConfidence(float f)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (modifyPositionWeights)
                _vrIk.solver.rightArm.positionWeight = Mathf.Lerp(_vrIk.solver.rightArm.positionWeight, GetTargetWeight(maxPositionWeight, f), interpolationSpeed * Time.deltaTime);
            if (modifyRotationWeights)
                _vrIk.solver.rightArm.rotationWeight = Mathf.Lerp(_vrIk.solver.rightArm.rotationWeight, GetTargetWeight(maxRotationWeight, f), interpolationSpeed * Time.deltaTime);
#endif
        }

        private void OnLeftArmConfidence(float f)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (modifyPositionWeights)
                _vrIk.solver.leftArm.positionWeight = Mathf.Lerp(_vrIk.solver.leftArm.positionWeight, GetTargetWeight(maxPositionWeight, f), interpolationSpeed * Time.deltaTime);
            if (modifyRotationWeights)
                _vrIk.solver.leftArm.rotationWeight = Mathf.Lerp(_vrIk.solver.leftArm.rotationWeight, GetTargetWeight(maxRotationWeight, f), interpolationSpeed * Time.deltaTime);
#endif
        }

        private void OnHipConfidence(float f)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (modifyPositionWeights)
                _vrIk.solver.spine.positionWeight = Mathf.Lerp(_vrIk.solver.spine.positionWeight, GetTargetWeight(maxPositionWeight, f), interpolationSpeed * Time.deltaTime);
            if (modifyRotationWeights)
                _vrIk.solver.spine.rotationWeight = Mathf.Lerp(_vrIk.solver.spine.rotationWeight, GetTargetWeight(maxRotationWeight, f), interpolationSpeed * Time.deltaTime);
#endif
        }

        private void OnHeadConfidence(float f)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (modifyPositionWeights)
                _vrIk.solver.spine.positionWeight = Mathf.Lerp(_vrIk.solver.spine.positionWeight, GetTargetWeight(maxPositionWeight, f), interpolationSpeed * Time.deltaTime);
            if (modifyRotationWeights)
                _vrIk.solver.spine.rotationWeight = Mathf.Lerp(_vrIk.solver.spine.rotationWeight, GetTargetWeight(maxRotationWeight, f), interpolationSpeed * Time.deltaTime);
#endif
        }
    }
}
#endif