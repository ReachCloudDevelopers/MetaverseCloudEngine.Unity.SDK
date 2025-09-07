#if MV_UNITY_AI_INFERENCE
using System;
using System.Collections.Generic;
using TMPro;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public sealed class YouOnlyLookOnceRenderer : TriInspectorMonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The YOLO component to listen to for detection frames.")]
        public YouOnlyLookOnce yolo;

        [Tooltip("UI container in which the rectangles will be drawn (e.g., a full-screen panel).")]
        public RectTransform canvasRoot;

        [Header("Coordinate Space")]
        [Tooltip("Which coordinate space the incoming detection Rects are in.")]
        public RectSpace inputSpace = RectSpace.ModelInput;

        [ShowIf(nameof(inputSpace), (int)RectSpace.ModelInput)]
        [Tooltip("Model input width (defaults to 640). Used to map detection Rects to the canvas when Input Space = ModelInput.")]
        public int modelInputWidth = 640;

        [ShowIf(nameof(inputSpace), (int)RectSpace.ModelInput)]
        [Tooltip("Model input height (defaults to 640). Used to map detection Rects to the canvas when Input Space = ModelInput.")]
        public int modelInputHeight = 640;

        [Header("Transforms / Orientation")]
        [Tooltip("Treat (0,0) as the top-left (common for images). If OFF, origin is bottom-left (Unity default UI space).")]
        public bool originTopLeft = true;

        [Tooltip("Flip X when mapping detection rectangles (useful if the visual feed is mirrored).")]
        public bool invertX;

        [Tooltip("Flip Y when mapping detection rectangles (useful for vertically flipped feeds).")]
        public bool invertY;

        [Header("Style")]
        [Tooltip("Pixel thickness of each rectangle border.")]
        [Range(1, 12)] public int borderThickness = 2;

        [Tooltip("Border color for all rectangles.")]
        public Color borderColor = new Color(0f, 0.84f, 1f, 0.95f);

        [Tooltip("Text color for the label in the top-right.")]
        public Color labelColor = Color.white;

        [Tooltip("Optional: show 'Label (score)' in the label text.")]
        public bool showScore = true;

        [Range(0, 1)] public float scoreRounding = 2;

        [Header("Pooling")]
        [Tooltip("Maximum pre-allocated items; more will be instantiated as needed.")]
        public int prewarm = 16;

        public enum RectSpace { ModelInput = 0, SourceTexture = 1 }

        // ─────────────────────────────────────────────────────────────────────────────

        private readonly List<Item> _pool = new();
        private int _liveCount;

        private void Awake()
        {
            if (!canvasRoot)
            {
                // If not set, attempt to use self.
                canvasRoot = transform as RectTransform;
            }

            EnsurePrewarm(prewarm);

            if (yolo) yolo.DetectionsFrame += OnDetectionsFrame;
        }

        private void OnEnable()
        {
            if (yolo) yolo.DetectionsFrame -= OnDetectionsFrame; // avoid double-sub
            if (yolo) yolo.DetectionsFrame += OnDetectionsFrame;
        }

        private void OnDisable()
        {
            if (yolo) yolo.DetectionsFrame -= OnDetectionsFrame;
        }

        private void OnDestroy()
        {
            if (yolo) yolo.DetectionsFrame -= OnDetectionsFrame;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Event handling
        // ─────────────────────────────────────────────────────────────────────────────

        private void OnDetectionsFrame(IReadOnlyList<YoloDetection> detections, int texW, int texH)
        {
            if (!canvasRoot) return;

            _liveCount = 0;

            var rootSize = canvasRoot.rect.size;
            var basisW = inputSpace == RectSpace.ModelInput ? modelInputWidth : Mathf.Max(1, texW);
            var basisH = inputSpace == RectSpace.ModelInput ? modelInputHeight : Mathf.Max(1, texH);

            for (int i = 0; i < detections.Count; i++)
            {
                var det = detections[i];
                var src = det.Rect; // src in chosen basis space

                // Convert from basis (e.g., 640x640) into canvasRoot pixel space
                // src.x, src.y are lower-left in basis if model generated that way.
                // Handle axis flips and origin selection.
                var mapped = MapRect(src, basisW, basisH, rootSize);

                var item = GetOrCreate(i);
                StyleItem(item);           // apply style (thickness/color/etc.)
                UpdateItem(item, det, mapped);
            }

            // Disable surplus pooled items
            for (int i = _liveCount; i < _pool.Count; i++)
                _pool[i].Root.gameObject.SetActive(false);
        }

        private Rect MapRect(Rect src, float basisW, float basisH, Vector2 rootSize)
        {
            // Normalize to [0..1] in basis
            float nx = src.x / basisW;
            float ny = src.y / basisH;
            float nw = src.width / basisW;
            float nh = src.height / basisH;

            // Apply flips in normalized space
            if (invertX) nx = 1f - nx - nw;
            if (invertY) ny = 1f - ny - nh;

            // Convert to pixel space of the canvas root
            float px = nx * rootSize.x;
            float py = ny * rootSize.y;
            float pw = nw * rootSize.x;
            float ph = nh * rootSize.y;

            // Handle top-left origin request
            if (originTopLeft)
            {
                // In Unity UI (bottom-left origin), move Y down from top
                py = rootSize.y - py - ph;
            }

            return new Rect(px, py, pw, ph);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Pooling & UI construction
        // ─────────────────────────────────────────────────────────────────────────────

        private void EnsurePrewarm(int count)
        {
            for (int i = _pool.Count; i < count; i++)
                _pool.Add(CreateItem(i));
        }

        private Item GetOrCreate(int index)
        {
            if (index >= _pool.Count)
                _pool.Add(CreateItem(index));

            var it = _pool[index];
            it.Root.gameObject.SetActive(true);
            _liveCount = Mathf.Max(_liveCount, index + 1);
            return it;
        }

        private Item CreateItem(int index)
        {
            var g = new GameObject($"det_{index:00}", typeof(RectTransform));
            var rt = g.GetComponent<RectTransform>();
            rt.SetParent(canvasRoot, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // Create 4 borders as child Images
            var top    = CreateBorder("top",    rt);
            var right  = CreateBorder("right",  rt);
            var bottom = CreateBorder("bottom", rt);
            var left   = CreateBorder("left",   rt);

            // Create TMP label at top-right
            var labelGO = new GameObject("label", typeof(RectTransform));
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.SetParent(rt, false);
            lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0f, 1f); // pivot at left-top so it grows leftwards
            lrt.anchoredPosition = Vector2.zero;

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 18;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;

            // Background for label for readability (optional)
            var labelBG = new GameObject("label_bg", typeof(RectTransform), typeof(Image));
            var bgRT = labelBG.GetComponent<RectTransform>();
            bgRT.SetParent(lrt, false);
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = new Vector2(-4, -2);
            bgRT.offsetMax = new Vector2(4, 2);
            var bgImg = labelBG.GetComponent<Image>();
            var c = Color.black; c.a = 0.5f;
            bgImg.color = c;
            bgImg.raycastTarget = false;
            // Make sure BG renders behind text
            bgRT.SetAsFirstSibling();

            return new Item
            {
                Root   = rt,
                Top    = top,
                Right  = right,
                Bottom = bottom,
                Left   = left,
                Label  = tmp,
                LabelBG = bgImg
            };
        }

        private static Image CreateBorder(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private void StyleItem(Item it)
        {
            it.Top.color = borderColor;
            it.Right.color = borderColor;
            it.Bottom.color = borderColor;
            it.Left.color = borderColor;
            it.Label.color = labelColor;
        }

        private void UpdateItem(Item it, YoloDetection det, Rect r)
        {
            // Position the root to the mapped rect
            it.Root.anchoredPosition = new Vector2(r.x, r.y);
            it.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, r.width);
            it.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   r.height);

            // Borders layout
            float t = Mathf.Max(1, borderThickness);

            // Top: (0, h - t) -> (w, t)
            it.Top.rectTransform.anchoredPosition = new Vector2(0, r.height - t);
            it.Top.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, r.width);
            it.Top.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   t);

            // Bottom: (0, 0) -> (w, t)
            it.Bottom.rectTransform.anchoredPosition = new Vector2(0, 0);
            it.Bottom.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, r.width);
            it.Bottom.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   t);

            // Left: (0, 0) -> (t, h)
            it.Left.rectTransform.anchoredPosition = new Vector2(0, 0);
            it.Left.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, t);
            it.Left.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   r.height);

            // Right: (w - t, 0) -> (t, h)
            it.Right.rectTransform.anchoredPosition = new Vector2(r.width - t, 0);
            it.Right.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, t);
            it.Right.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   r.height);

            // Label text (top-right)
            if (showScore)
            {
                float rounded = (float)Math.Round(det.Score * 100f, (int)scoreRounding);
                it.Label.text = $"{det.Label} ({rounded:0.#}%)";
            }
            else
            {
                it.Label.text = det.Label;
            }

            // Keep label inside bounds with a small pad
            var pad = new Vector2(4f, -4f);
            var lrt = it.Label.rectTransform;
            lrt.anchoredPosition = new Vector2(0f, 0f) + pad;
        }

        // ─────────────────────────────────────────────────────────────────────────────

        private class Item
        {
            public RectTransform Root;
            public Image Top;
            public Image Right;
            public Image Bottom;
            public Image Left;
            public TextMeshProUGUI Label;
            public Image LabelBG;
        }
    }
}
#else
namespace MetaverseCloudEngine.Unity.AI.Components
{
    public class YouOnlyLookOnceRenderer : InferenceEngineComponent {}
}
#endif
