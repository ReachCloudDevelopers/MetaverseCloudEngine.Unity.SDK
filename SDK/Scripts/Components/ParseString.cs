using System;
using System.Text.RegularExpressions;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class ParseString : TriInspectorMonoBehaviour
    {
        [Serializable]
        public class ParseMatch
        {
            [Header("Uses Regex")]
            public string matchExpression;
            public string replaceExpression;
            public UnityEvent<string> onResult;
            public UnityEvent<float> onResultFloat;
            public UnityEvent<int> onResultInt;
            public UnityEvent onMatchFailed;
        }

        [SerializeField] private string stringValue;
        public bool parseOnStart = true;
        public bool parseOnChange = true;
        public ParseMatch[] matches;

        private bool _hasStarted;

        public string StringValue {
            get => stringValue;
            set {
                if (stringValue != value)
                {
                    stringValue = value;
                    if (parseOnChange)
                        Parse();
                }
            }
        }

        private void Start()
        {
            _hasStarted = true;
            if (parseOnStart)
                Parse();
        }
        
        public void Parse(string value)
        {
            stringValue = value;
            Parse();
        }

        public void Parse()
        {
            if (!_hasStarted && parseOnStart)
                return;

            if (!isActiveAndEnabled)
                return;

            foreach (var match in matches)
            {
                if (Regex.Match(stringValue, match.matchExpression).Success)
                {
                    var value = Regex.Replace(stringValue, match.matchExpression, match.replaceExpression);
                    match.onResult?.Invoke(value);
                    
                    if (match.onResultFloat?.GetPersistentEventCount() > 0)
                    {
                        if (float.TryParse(value, out var floatValue))
                            match.onResultFloat.Invoke(floatValue);
                    }
                    
                    if (match.onResultInt?.GetPersistentEventCount() > 0)
                    {
                        if (int.TryParse(value, out var intValue))
                            match.onResultInt.Invoke(intValue);
                    }
                }
                else
                    match.onMatchFailed?.Invoke();
            }
        }
    }
}
