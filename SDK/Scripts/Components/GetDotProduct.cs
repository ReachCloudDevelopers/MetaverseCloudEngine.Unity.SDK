using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [DeclareFoldoutGroup("References (Optional)")]
    [HideMonoScript]
    public class GetDotProduct : TriInspectorMonoBehaviour
    {
        public enum SourceAxis
        {
            Forward,
            Up,
            Right
        }

        public enum TargetMode
        {
            Position,
            Direction
        }
        
        public enum TargetDirection
        {
            Forward,
            Up,
            Right
        }

        [Required]
        public Transform target;
        public SourceAxis sourceAxis;
        public TargetMode targetMode;
        [ShowIf(nameof(targetMode), TargetMode.Direction)]
        public TargetDirection targetDirection;
        public UnityEvent<float> onOutputDotProduct;
        [Group("References (Optional)")] public Transform source;

        [ReadOnly]
        [ShowInInspector]
        public float OutputDotProduct { get; private set; }

        private void Start()
        {
            if (source == null)
                source = transform;
        }

        private void Update()
        {
            Tick();
        }

        private void FixedUpdate()
        {
            Tick();
        }

        private void Tick()
        {
            try
            {
                var sourcePosition = source.position;
                Vector3 direction;

                if (targetMode == TargetMode.Position)
                {
                    var targetPosition = target.position;
                    direction = targetPosition - sourcePosition;   
                }
                else
                {
                    direction = targetDirection switch
                    {
                        TargetDirection.Forward => target.forward,
                        TargetDirection.Up => target.up,
                        TargetDirection.Right => target.right,
                        _ => target.forward
                    };
                }
                
                var forward = source.forward;
                var sourceDirection = sourceAxis switch
                {
                    SourceAxis.Forward => forward,
                    SourceAxis.Up => source.up,
                    SourceAxis.Right => source.right,
                    _ => forward
                };
                OutputDotProduct = Vector3.Dot(sourceDirection, direction.normalized);
                onOutputDotProduct?.Invoke(OutputDotProduct);
            }
            catch (NullReferenceException e)
            {
                MetaverseProgram.Logger.LogError(e);
                enabled = false;
            }
            catch (MissingReferenceException e)
            {
                MetaverseProgram.Logger.LogError(e);
                enabled = false;
            }
        }
    }
}