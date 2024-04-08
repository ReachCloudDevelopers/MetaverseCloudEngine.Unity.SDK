using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class LayoutHelper : MonoBehaviour
    {
        [FormerlySerializedAs("updateCanvasesOnEnable")]
        public bool updateOnEnable = true;
        [FormerlySerializedAs("updateChildCanvasesOnly")]
        public bool updateChildrenOnly = true;
        public bool layoutImmediately = true;

        private void OnEnable()
        {
            if (updateOnEnable)
                Layout();
        }

        public void Layout()
        {
            if (!isActiveAndEnabled)
                return;
            
            if (!layoutImmediately && Application.isPlaying)
                StartCoroutine(UpdateLayoutDelayed());
            else
                LayoutImmediate();
        }

        [Obsolete("Please use " + nameof(Layout) + " instead.")]
        public void UpdateCanvases() => Layout();

        private IEnumerator UpdateLayoutDelayed()
        {
            yield return new WaitForEndOfFrame();
            const int maxRectsToUpdate = 10;
            var rectTransforms = GetUpdatableTransforms();
            
            var canvas = GetComponentInParent<Canvas>();
            if (MVUtils.IsVRCompatible() && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                yield break;
            
            for (var i = 0; i < rectTransforms.Length; i++)
            {
                var rt = rectTransforms[i];
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                if (i % maxRectsToUpdate == 0)
                    yield return null;
            }
        }

        private void LayoutImmediate()
        {
            Canvas.ForceUpdateCanvases();
            var rectTransforms = GetUpdatableTransforms();
            foreach (var rt in rectTransforms)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private RectTransform[] GetUpdatableTransforms()
        {
            return !updateChildrenOnly ? 
                FindObjectsOfType<RectTransform>() : 
                transform.parent ? 
                    transform.parent.GetComponentsInChildren<RectTransform>() : 
                    GetComponentsInChildren<RectTransform>();
        }
    }
}