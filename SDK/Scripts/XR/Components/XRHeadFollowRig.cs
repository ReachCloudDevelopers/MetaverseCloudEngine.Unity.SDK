using System;
using MetaverseCloudEngine.Unity.Attributes;
using System.Collections;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [HideMonoScript]
    public class XRHeadFollowRig : TriInspectorMonoBehaviour
    {
        [Header("Player")]
        [DisallowNull] public Transform xrRoot;
        [DisallowNull] public Transform xrHead;

        [Header("Settings")]
        public float moveTime = 1f;
        public float angleThreshold = 60;
        public float positionThreshold = 0.25f;

        private Quaternion _lastRot;
        private Vector3 _lastPos;
        private IEnumerator _currentMoveOp;

        private void OnEnable()
        {
            Tick(true);
        }

        private void OnDisable()
        {
            CancelMove();
        }

        private void Update()
        {
            Tick();
        }

        private void Tick(bool forceUpdate = false)
        {
            try
            {
                Quaternion targetRot = xrHead.rotation;
                Vector3 targetPos = xrRoot.InverseTransformPoint(xrHead.position);
                targetPos.y = 0;
                targetPos = xrRoot.TransformPoint(targetPos);

                if (forceUpdate || Mathf.Abs(Quaternion.Angle(_lastRot, targetRot)) > angleThreshold || Vector3.Distance(targetPos, _lastPos) > positionThreshold)
                {
                    Move(targetPos, targetRot);
                    _lastRot = targetRot;
                    _lastPos = targetPos;
                }
            }
            catch (Exception e)
            {
                enabled = false;
            }
        }

        public void Move(Vector3 targetPosition, Quaternion targetRotation)
        {
            CancelMove();
            _currentMoveOp = MoveAsync(targetPosition, targetRotation);
            StartCoroutine(_currentMoveOp);
        }

        private void CancelMove()
        {
            if (_currentMoveOp != null)
            {
                StopCoroutine(_currentMoveOp);
                _currentMoveOp = null;
            }
        }

        private IEnumerator MoveAsync(Vector3 targetPosition, Quaternion targetRotation)
        {
            float startTime = MVUtils.CachedTime;
            float endTime = MVUtils.CachedTime + moveTime;

            targetRotation = FlattenRotation(targetRotation);

            while (MVUtils.CachedTime <= endTime)
            {
                float t = Mathf.InverseLerp(startTime, endTime, MVUtils.CachedTime);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPosition, t), Quaternion.Lerp(transform.rotation, targetRotation, t));
                yield return null;
            }
        }

        private Quaternion FlattenRotation(Quaternion targetRotation)
        {
            Vector3 fwd = (targetRotation * Vector3.forward).FlattenDirection(xrRoot.up);
            targetRotation = Quaternion.LookRotation(fwd, xrRoot.up);
            return targetRotation;
        }
    }
}
