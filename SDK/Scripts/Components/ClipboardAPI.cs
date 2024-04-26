using TMPro;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// This component allows you to interact with the devices system clipboard.
    /// </summary>
    [HideMonoScript]
    public partial class ClipboardAPI : TriInspectorMonoBehaviour
    {
        [Tooltip("If checked, share functionality will be performed for mobile platforms.")]
        public bool useMobileShare = true;

        private string _stringData;

        /// <summary>
        /// Set the string data that should be copied whenever <see cref="Copy"/> is called.
        /// </summary>
        /// <param name="stringData">The data to pass into the clipboard copy.</param>
        public void SetString(string stringData)
        {
            _stringData = stringData;
        }

        /// <summary>
        /// Copies the string data given through the <see cref="SetString(string)"/> method.
        /// </summary>
        public void Copy()
        {
            if (!string.IsNullOrWhiteSpace(_stringData))
                Copy(_stringData);
        }

        /// <summary>
        /// Copies the text from the <paramref name="text"/> component.
        /// </summary>
        /// <param name="text">The text component to copy from.</param>
        public void CopyTmpText(TMP_Text text)
        {
            if (text != null && !string.IsNullOrWhiteSpace(text.text))
                Copy(text.text);
        }

        /// <summary>
        /// Copies the text from the <paramref name="inputField"/> component.
        /// </summary>
        /// <param name="inputField">The input field to copy from.</param>
        public void CopyTmpInputField(TMP_InputField inputField)
        {
            if (inputField != null && !string.IsNullOrWhiteSpace(inputField.text))
                Copy(inputField.text);
        }

        /// <summary>
        /// Copy the give <paramref name="text"/> string.
        /// </summary>
        /// <param name="text">The text to copy.</param>
        public void Copy(string text)
        {
            bool copied = false;
            CopyInternal(text, ref copied);
            if (!copied)
                GUIUtility.systemCopyBuffer = text;
        }

        /// <summary>
        /// Paste the clipboard text into the given <paramref name="inputField"/>.
        /// </summary>
        /// <param name="inputField">The input field to paste the text into.</param>
        public void PasteTmpInputField(TMP_InputField inputField)
        {
            if (!MetaverseProgram.IsCoreApp)
                inputField.text = GUIUtility.systemCopyBuffer;
            else PasteInternal(inputField);
        }

        /// <summary>
        /// Paste the clipboard text into the given <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The text object to paste the clipboard text into.</param>
        public void PasteTmpText(TMP_Text text)
        {
            if (!MetaverseProgram.IsCoreApp)
                text.text = GUIUtility.systemCopyBuffer;
            else PasteInternal(text);
        }

        partial void CopyInternal(string text, ref bool copied);

        partial void PasteInternal(TMP_InputField inputField);

        partial void PasteInternal(TMP_Text text);
    }
}
