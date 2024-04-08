using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component that allows you to modify 2 numbers.
    /// </summary>
    public class ModifyNumber : MonoBehaviour
    {
        /// <summary>
        /// The operation type.
        /// </summary>
        public enum OperationType
        {
            /// <summary>
            /// Add the 2 numbers.
            /// </summary>
            Add,
            /// <summary>
            /// Subtract the 2 numbers.
            /// </summary>
            Subtract,
            /// <summary>
            /// Multiply the 2 numbers.
            /// </summary>
            Multiply,
            /// <summary>
            /// Divide the 2 numbers.
            /// </summary>
            Divide,
            /// <summary>
            /// 1 number to the power of another.
            /// </summary>
            Exponent,
            /// <summary>
            /// 1 number log another number.
            /// </summary>
            Log
        }

        /// <summary>
        /// The side of the operand.
        /// </summary>
        public enum OperandSide
        {
            /// <summary>
            /// Whether the operand should be on the left side.
            /// </summary>
            Left,
            /// <summary>
            /// Whether the operand should be on the right side.
            /// </summary>
            Right,
        }

        [Tooltip("The operand on the specified side of the equation.")]
        [SerializeField] private float operand;
        [Tooltip("The side of the operand in the equation.")]
        [SerializeField] private OperandSide operandSide;
        [Tooltip("The type of operation to perform on the operand.")]
        [SerializeField] private OperationType operation;

        [Header("Events")]
        [Tooltip("Outputs the result of the operation as a float.")]
        public UnityEvent<float> onFloatResult;
        [Tooltip("Outputs the result of the operation as an integer.")]
        public UnityEvent<int> onIntResult;

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public int OperationTypeValue {
            get => (int)operation;
            set => operation = (OperationType)value;
        }

        /// <summary>
        /// Gets or sets the operation side.
        /// </summary>
        public int OperandSideValue {
            get => (int)operandSide;
            set => operandSide = (OperandSide)value;
        }

        /// <summary>
        /// Gets or sets the operand value.
        /// </summary>
        public float Operand {
            get { return operand; }
            set { operand = value; }
        }

        /// <summary>
        /// Gets or sets the operand value (as an integer).
        /// </summary>
        public int OperandInt {
            get { return (int)operand; }
            set { operand = value; }
        }

        /// <summary>
        /// Perform the modification operation.
        /// </summary>
        /// <param name="value">The value to perform the operation with.</param>
        public void Perform(float value)
        {
            switch (operation)
            {
                case OperationType.Add:
                    switch (operandSide)
                    {
                        case OperandSide.Left:
                            OnValue(operand + value);
                            break;
                        case OperandSide.Right:
                            OnValue(value + operand);
                            break;
                    }
                    break;
                case OperationType.Subtract:
                    switch (operandSide)
                    {
                        case OperandSide.Left:
                            OnValue(operand - value);
                            break;
                        case OperandSide.Right:
                            OnValue(value - operand);
                            break;
                    }
                    break;
                case OperationType.Multiply:
                    OnValue(operand * value);
                    break;
                case OperationType.Divide:
                    switch (operandSide)
                    {
                        case OperandSide.Left:
                            if (value != 0)
                                OnValue(operand / value);
                            else
                                OnValue(0);
                            break;
                        case OperandSide.Right:
                            if (operand != 0)
                                OnValue(value / operand);
                            else
                                OnValue(0);
                            break;
                    }
                    break;
                case OperationType.Exponent:
                    switch (operandSide)
                    {
                        case OperandSide.Left:
                            OnValue(Mathf.Pow(operand, value));
                            break;
                        case OperandSide.Right:
                            OnValue(Mathf.Pow(value, operand));
                            break;
                    }
                    break;
                case OperationType.Log:
                    switch (operandSide)
                    {
                        case OperandSide.Left:
                            OnValue(Mathf.Log(operand, value));
                            break;
                        case OperandSide.Right:
                            OnValue(Mathf.Log(value, operand));
                            break;
                    }
                    break;
            }
        }

        /// <summary>
        /// Perform the modification operation.
        /// </summary>
        /// <param name="value">The value to perform the operation with.</param>
        public void Perform(int value) => Perform((float)value);

        private void OnValue(float v)
        {
            onFloatResult.Invoke(v);
            onIntResult?.Invoke((int)v);
        }
    }
}
