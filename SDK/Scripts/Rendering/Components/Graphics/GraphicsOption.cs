using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    public abstract class GraphicsOption : MonoBehaviour
    {
        public TextMeshProUGUI subOptionText;
        public List<GraphicsSubOption> SubOptionList { get; set; } = new();
        public GraphicsSubOption CurrentGraphicsSubOption { get; set; }
        public int CurrentSubOptionIndex { get; set; }

        protected virtual void Awake()
        {
            Initialize();
        }

        public abstract void Apply();

        protected virtual void Initialize()
        {
        }

        public void UpdateSuboptionText()
        {
            if (CurrentGraphicsSubOption != null)
            {
                subOptionText.text = CurrentGraphicsSubOption.name;
                return;
            }

            Debug.LogError("Current suboption is null in : " + gameObject.name);
        }

        public void SelectNextSubOption()
        {
            CurrentSubOptionIndex = GetNextSuboptionIndex();
            CurrentGraphicsSubOption = SubOptionList[CurrentSubOptionIndex];
            UpdateSuboptionText();
        }

        public void SelectPreviousSubOption()
        {
            CurrentSubOptionIndex = GetPreviousSubOptionIndex();
            CurrentGraphicsSubOption = SubOptionList[CurrentSubOptionIndex];
            UpdateSuboptionText();
        }

        private int GetNextSuboptionIndex() => GetNextValue(CurrentSubOptionIndex, SubOptionList.Count);

        private int GetPreviousSubOptionIndex() => GetPreviousValue(CurrentSubOptionIndex, SubOptionList.Count);

        private int GetNextValue(int currentVal, int maxVal)
        {
            if (currentVal >= maxVal - 1) return 0;
            return currentVal + 1;
        }

        private int GetPreviousValue(int currentVal, int maxVal)
        {
            if (currentVal == 0) return maxVal - 1;
            return currentVal - 1;
        }

        public void SelectDefaultValue()
        {
            CurrentSubOptionIndex = SubOptionList.Count - 1;
            CurrentGraphicsSubOption = SubOptionList[CurrentSubOptionIndex];
            UpdateSuboptionText();
        }
    }
}

