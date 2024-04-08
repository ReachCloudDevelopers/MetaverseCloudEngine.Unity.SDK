using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [AddComponentMenu("UI/Effects/DropShadow", 14)]
    public class DropShadow : BaseMeshEffect
    {
        [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.5f);
        [SerializeField] private Vector2 shadowDistance = new Vector2(1f, -1f);
        [FormerlySerializedAs("usesGraphicAlpha")] [SerializeField] private bool useGraphicAlpha = true;
        
        public int iterations = 5;
        public Vector2 shadowSpread = Vector2.one;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            EffectDistance = shadowDistance;
            base.OnValidate();
        }

#endif

        public Color EffectColor
        {
            get => shadowColor;
            set
            {
                shadowColor = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public Vector2 ShadowSpread
        {
            get => shadowSpread;
            set
            {
                shadowSpread = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public int Iterations
        {
            get => iterations;
            set
            {
                iterations = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public Vector2 EffectDistance
        {
            get => shadowDistance;
            set
            {
                shadowDistance = value;

                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public bool UseGraphicAlpha
        {
            get => useGraphicAlpha;
            set
            {
                useGraphicAlpha = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        private void DropShadowEffect(ICollection<UIVertex> verts)
        {
            var count = verts.Count;
            var vertsCopy = new List<UIVertex>(verts);
            verts.Clear();

            for (var i = 0; i < iterations; i++)
            {
                for (var v = 0; v < count; v++)
                {
                    var vt = vertsCopy[v];
                    var position = vt.position;
                    var fac = i / (float)iterations;
                    position.x *= 1 + shadowSpread.x * fac * 0.01f;
                    position.y *= 1 + shadowSpread.y * fac * 0.01f;
                    position.x += shadowDistance.x * fac;
                    position.y += shadowDistance.y * fac;
                    vt.position = position;
                    Color32 color = shadowColor;
                    color.a = (byte)(color.a / (float)iterations);
                    vt.color = color;
                    verts.Add(vt);
                }
            }

            foreach (var t in vertsCopy)
                verts.Add(t);
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            var output = new List<UIVertex>();
            vh.GetUIVertexStream(output);

            DropShadowEffect(output);

            vh.Clear();
            vh.AddUIVertexTriangleStream(output);
        }
    }
}