using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [AddComponentMenu("")]
    public class UnityAnimatorIKCallbacks : MonoBehaviour
    {
        public delegate void AnimatorIKCallback(int layerIndex);

        private List<(int order, AnimatorIKCallback callback)> _onAnimatorIkCallbacks = new ();

        private void Start() { /* for enabled/disabled toggle */ }

        public void RegisterIKCallback(AnimatorIKCallback callback, int order = 0)
        {
            _onAnimatorIkCallbacks.Add((order, callback));
            _onAnimatorIkCallbacks = _onAnimatorIkCallbacks.OrderBy(x => x.order).ToList();
        }

        public void UnRegisterIKCallback(AnimatorIKCallback callback)
        {
            if (_onAnimatorIkCallbacks.RemoveAll(x => x.callback == callback) > 0)
                _onAnimatorIkCallbacks = _onAnimatorIkCallbacks.OrderBy(x => x.order).ToList();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            for (var i = 0; i < _onAnimatorIkCallbacks.Count; i++)
                _onAnimatorIkCallbacks[i].callback?.Invoke(layerIndex);
        }
    }
}
