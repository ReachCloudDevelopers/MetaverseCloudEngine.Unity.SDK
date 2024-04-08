using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Scripting;

namespace MetaverseCloudEngine.Unity
{
    public abstract class SingletonScriptableObject<T> : TriInspectorScriptableObject where T : SingletonScriptableObject<T>
    {
        private static T _instance;
        public static T Instance {
            get {
                if (!_instance)
                    LoadInstance();
                return _instance;
            }
        }

        [Preserve]
        public static void LoadInstance()
        {
            _instance = Resources.LoadAll<T>(string.Empty).FirstOrDefault();
        }
    }
}
