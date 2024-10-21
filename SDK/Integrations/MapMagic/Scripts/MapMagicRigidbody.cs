#if MAPMAGIC2

using MapMagic.Core;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.MapMagic
{
    [RequireComponent(typeof(Rigidbody))]
    public class MapMagicRigidbody : MonoBehaviour
    {
        private static MapMagicObject _mapMagicObj;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            if (!_mapMagicObj)
            {
#if UNITY_6000_0_OR_NEWER
                _mapMagicObj = FindFirstObjectByType<MapMagicObject>(FindObjectsInactive.Include);
#else
                _mapMagicObj = FindObjectOfType<MapMagicObject>(true);
#endif
            }
            else
            {
                enabled = false;
                Destroy(this);
            }
        }

        private void Update()
        {
            FreezeRigidbody();
        }

        private void FreezeRigidbody()
        {
            if (_mapMagicObj.IsGenerating())
            {
                if (!_rigidbody) _rigidbody = GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
                _rigidbody.linearVelocity = Vector3.zero;
#else
                _rigidbody.velocity = Vector3.zero;
#endif
                _rigidbody.MovePosition(Vector3.zero);
                _rigidbody.Sleep();
            }
            else
            {
                Destroy(this);
            }
        }
    }
}

#endif