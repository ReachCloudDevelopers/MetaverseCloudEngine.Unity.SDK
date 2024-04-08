using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    public class ParseString : MonoBehaviour
    {
        [Serializable]
        public class ParseMatch
        {
            [Header("Uses Regex")]
            public string matchExpression;
            public string replaceExpression;
            public UnityEvent<string> onResult;
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

        public void Parse()
        {
            if (!_hasStarted && parseOnStart)
                return;

            if (!isActiveAndEnabled)
                return;

            foreach (ParseMatch match in matches)
            {
                if (Regex.Match(stringValue, match.matchExpression).Success)
                {
                    string value = Regex.Replace(stringValue, match.matchExpression, match.replaceExpression);
                    match.onResult?.Invoke(value);
                }
                else
                    match.onMatchFailed?.Invoke();
            }
        }
    }
}
