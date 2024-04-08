using MetaverseCloudEngine.Unity.Attributes;
using Newtonsoft.Json;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    public abstract partial class LandPlotObjectPropertyBase<T> : MonoBehaviour, ILandPlotObjectPropertyOfType<T>
    {
        public delegate void ValueChangedDelegate(T oldValue, T newValue);

        [Serializable]
        public class Events
        {
            public UnityEvent<T> onValue = new();
            public UnityEvent onNullValue = new();
            public UnityEvent<string> validationFailed = new();
        }

        [Serializable]
        public class Descriptors
        {
            [DisallowNull] public string displayName;
            public string description;
        }

        [SerializeField, DisallowNull] private string key;
        [SerializeField] private T value;
        [SerializeField] private bool hidden;

        public Descriptors descriptors = new();
        public Events events = new();

        public event Action<object> ValueChanged;
        public event Action<string> ValidationFailed;

        public string Key {
            get => key;
            set => key = value;
        }
        public T Value {
            get => value;
            set => Set(value);
        }
        public object RawValue => value;
        public string DisplayName => descriptors.displayName;
        public string Description => descriptors.description;
        public bool Hidden => hidden;

        public virtual void Set(object obj)
        {
            if (obj is not T newValue)
            {
                try { newValue = (T)Convert.ChangeType(obj, typeof(T)); }
                catch { return; }
            }

            if (IsChanged(this.value, newValue))
            {
                if (!Validate(newValue, out string error))
                {
                    ValidationFailed?.Invoke(error);
                    events.validationFailed?.Invoke(error);
                    return;
                }

                T oldValue = this.value;
                this.value = newValue;
                if (obj is null) events.onNullValue?.Invoke();
                else events.onValue?.Invoke(newValue);
                ValueChanged?.Invoke(newValue);
                OnValueChanged(oldValue, newValue);
                OnValueChangedInternal(newValue);
            }
        }
        
        protected abstract bool IsChanged(T oldValue, T newValue);

        partial void OnValueChangedInternal(T value);

        protected virtual void OnValueChanged(T oldValue, T newValue)
        {
        }

        public virtual bool Validate(T value, out string error)
        {
            error = null;
            return true;
        }

        public void SetValueWithoutNotify(T value)
        {
            this.value = value;
        }
    }
}
