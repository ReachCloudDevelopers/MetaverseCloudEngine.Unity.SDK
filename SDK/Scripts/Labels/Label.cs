using System;
using System.Threading;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Labels
{
    /// <summary>
    /// [Deprecated] Labels can be used to easily reference important strings across objects.
    /// </summary>
    [Serializable]
    public class Label
    {
        [SerializeField] private string value;

        public bool HasReference => false;

        public void SetValue(string v)
        {
            value = v;
        }

        public void GetValueAsync(Action<string> onValue, CancellationToken cancellationToken = default)
        {
            onValue?.Invoke(value);
        }

        public static void GetAllAsync(Label[] labels, Action<string[]> values, CancellationToken cancellationToken = default)
        {
            var labelCount = labels.Length;
            var numLoadedLabels = 0;
            var output = new string[labelCount];

            for (var i = 0; i < labels.Length; i++)
            {
                var idx = i;
                labels[idx].GetValueAsync(s =>
                {
                    output[idx] = s;
                    numLoadedLabels++;

                }, cancellationToken);
            }

            MetaverseDispatcher.WaitUntil(
                () => cancellationToken.IsCancellationRequested || numLoadedLabels == labelCount,
                () =>
                {
                    values?.Invoke(output);
                });
        }

        public static void GetAllAsync<T>(T[] sourceArray, Func<T, Label> getLabel, Action<Tuple<T, string>[]> values, CancellationToken cancellationToken = default)
        {
            var labelCount = sourceArray.Length;
            var numLoadedLabels = 0;
            var output = new Tuple<T, string>[labelCount];

            for (var i = 0; i < sourceArray.Length; i++)
            {
                var label = getLabel(sourceArray[i]);
                var idx = i;

                if (label == null)
                    output[i] = null;
                else label.GetValueAsync(s =>
                {
                    output[idx] = new Tuple<T, string>(sourceArray[idx], s);
                    numLoadedLabels++;

                }, cancellationToken);
            }

            MetaverseDispatcher.WaitUntil(
                () => cancellationToken.IsCancellationRequested || numLoadedLabels == labelCount, 
                () =>
                {
                    values?.Invoke(output);
                });
        }

        public static explicit operator string(Label label)
        {
            return label?.value;
        }

        public static implicit operator Label(string str)
        {
            return new Label
            {
                value = str,
            };
        }

        public override string ToString()
        {
            return value ?? "Not Loaded";
        }
    }
}
