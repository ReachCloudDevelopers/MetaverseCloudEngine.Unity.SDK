using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    public class GridLayoutGroupEvents : UIBehaviour
    {
        public UnityEvent<int> onColumnCountChanged;
        public UnityEvent<int> onRowCountChanged;

        private int _columnCount;
        private int _rowCount;

        protected override void OnRectTransformDimensionsChange()
        {
            GetColumnAndRow(out int columnCount, out int rowCount);

            if (columnCount != _columnCount)
            {
                _columnCount = columnCount;
                onColumnCountChanged?.Invoke(_columnCount);
            }

            if (rowCount != _rowCount)
            {
                _rowCount = rowCount;
                onRowCountChanged?.Invoke(_rowCount);
            }
        }

        private void GetColumnAndRow(out int column, out int row)
        {
            column = 0;
            row = 0;

            RectTransform rt = (RectTransform) transform;
            if (rt.childCount == 0)
                return;

            //Column and row are now 1
            column = 1;
            row = 1;

            //Get the first child GameObject of the GridLayoutGroup
            RectTransform firstChildObj = rt.GetChild(0).GetComponent<RectTransform>();

            Vector2 firstChildPos = firstChildObj.anchoredPosition;
            bool stopCountingRow = false;

            //Loop through the rest of the child object
            for (int i = 1; i < rt.childCount; i++)
            {
                //Get the next child
                RectTransform currentChildObj = rt.GetChild(i).GetComponent<RectTransform>();
                Vector2 currentChildPos = currentChildObj.anchoredPosition;

                //if first child.x == otherChild.x, it is a column, ele it's a row
                if (Math.Abs(firstChildPos.x - currentChildPos.x) < 0.0001f)
                {
                    column++;
                    //Stop counting row once we find column
                    stopCountingRow = true;
                }
                else
                {
                    if (!stopCountingRow)
                        row++;
                }
            }
        }
    }
}