using UnityEngine;
using UnityEngine.UI;

namespace ParodyStudios.Cricket
{
    /// <summary>
    /// Optional helper that procedurally creates the colored meter strip
    /// (Red → Yellow → Green → Blue → Green → Yellow → Red) as child UI Images.
    ///
    /// Attach this to the meter's "Track" RectTransform. Click "Rebuild" in the
    /// inspector context menu, or it will rebuild automatically on Awake.
    ///
    /// This saves you from manually slicing the meter sprite — the colors and
    /// proportions match the spec exactly:
    ///     Red    → 0%
    ///     Yellow → ~40%
    ///     Green  → ~70%
    ///     Blue   → 100%   (center, symmetrical)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BowlingMeterTrackBuilder : MonoBehaviour
    {
        [Header("Colors")]
        public Color redColor    = new Color(0.93f, 0.18f, 0.18f);
        public Color yellowColor = new Color(0.97f, 0.78f, 0.16f);
        public Color greenColor  = new Color(0.20f, 0.85f, 0.32f);
        public Color blueColor   = new Color(0.18f, 0.42f, 0.97f);

        [Tooltip("Width of the colored bar in pixels (track height comes from this RectTransform).")]
        public float barWidth = 28f;

        [Tooltip("Rebuild the strip automatically on Awake (so it appears in Play mode).")]
        public bool buildOnAwake = true;

        // Symmetric segment proportions (must sum to 1.0 across all 7 zones).
        // From top to bottom: Red, Yellow, Green, Blue (center), Green, Yellow, Red.
        private static readonly float[] SegmentWeights = { 0.15f, 0.13f, 0.12f, 0.20f, 0.12f, 0.13f, 0.15f };

        private void Awake()
        {
            if (buildOnAwake) Rebuild();
        }

        [ContextMenu("Rebuild Meter Strip")]
        public void Rebuild()
        {
            // Clean existing children that we created previously.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name.StartsWith("MeterSegment_"))
#if UNITY_EDITOR
                    DestroyImmediate(c.gameObject);
#else
                    Destroy(c.gameObject);
#endif
            }

            RectTransform rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(barWidth, rt.sizeDelta.y);

            float totalHeight = rt.rect.height;
            float topY = totalHeight * 0.5f;

            Color[] colors = { redColor, yellowColor, greenColor, blueColor, greenColor, yellowColor, redColor };

            for (int i = 0; i < SegmentWeights.Length; i++)
            {
                float segH = SegmentWeights[i] * totalHeight;
                float centerY = topY - segH * 0.5f;
                topY -= segH;

                GameObject seg = new GameObject($"MeterSegment_{i}", typeof(RectTransform), typeof(Image));
                seg.transform.SetParent(transform, false);

                RectTransform sr = (RectTransform)seg.transform;
                sr.anchorMin = new Vector2(0.5f, 0.5f);
                sr.anchorMax = new Vector2(0.5f, 0.5f);
                sr.pivot     = new Vector2(0.5f, 0.5f);
                sr.sizeDelta = new Vector2(barWidth, segH);
                sr.anchoredPosition = new Vector2(0f, centerY);

                seg.GetComponent<Image>().color = colors[i];
            }
        }
    }
}
