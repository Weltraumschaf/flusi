using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// Builds a round gauge face at Awake: tick marks and numeric labels placed
    /// around a circle. No texture assets — ticks are plain Image rectangles and
    /// labels use Unity's built-in font.
    ///
    /// Angle convention as GaugeScale: degrees clockwise from 12 o'clock.
    public class GaugeFaceBuilder : MonoBehaviour
    {
        [Header("Arc")]
        [SerializeField] private float startAngle = 0f;
        [SerializeField] private float sweepAngle = 320f;

        [Header("Ticks")]
        [SerializeField] private int tickCount = 21;      // inclusive of both ends
        [SerializeField] private int majorEvery = 4;
        [SerializeField] private float radius = 60f;
        [SerializeField] private float minorLength = 6f;
        [SerializeField] private float majorLength = 12f;
        [SerializeField] private float tickWidth = 2f;
        [SerializeField] private Color tickColor = Color.white;

        [Header("Labels")]
        [SerializeField] private bool showLabels = true;
        [SerializeField] private float labelMinValue = 0f;
        [SerializeField] private float labelMaxValue = 500f;
        [SerializeField] private float labelRadius = 42f;
        [SerializeField] private int labelFontSize = 10;

        private const string TickName = "Tick";
        private const string LabelName = "Label";

        // Unity calls Awake regardless of the component's enabled state, so a
        // disabled builder (the gauges that swapped in generated face art)
        // must skip Build() explicitly or it silently rebuilds its ticks and
        // labels on top of that art every time the scene loads.
        private void Awake()
        {
            if (enabled) Build();
        }

        private void Build()
        {
            // Guard against rebuilding on top of children this component already
            // created — e.g. a Play Mode object-override apply can bake generated
            // ticks/labels into the prefab, and the next Awake would otherwise
            // duplicate them silently.
            ClearBuiltChildren();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < tickCount; i++)
            {
                float t = tickCount <= 1 ? 0f : i / (float)(tickCount - 1);
                float angle = startAngle + t * sweepAngle;
                bool major = majorEvery > 0 && i % majorEvery == 0;

                CreateTick(angle, major ? majorLength : minorLength);

                if (showLabels && major)
                    CreateLabel(angle, Mathf.Lerp(labelMinValue, labelMaxValue, t), font);
            }
        }

        private void ClearBuiltChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != TickName && child.name != LabelName)
                    continue;

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private void CreateTick(float angle, float length)
        {
            var go = new GameObject(TickName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.sizeDelta = new Vector2(tickWidth, length);
            rt.anchoredPosition = PointAt(angle, radius - length * 0.5f);
            rt.localEulerAngles = new Vector3(0f, 0f, -angle);

            var image = go.GetComponent<Image>();
            image.color = tickColor;
            image.raycastTarget = false;
        }

        private void CreateLabel(float angle, float value, Font font)
        {
            var go = new GameObject(LabelName, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.sizeDelta = new Vector2(40f, 16f);
            rt.anchoredPosition = PointAt(angle, labelRadius);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = labelFontSize;
            text.color = tickColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = Mathf.RoundToInt(value).ToString();
        }

        /// Point at `angle` degrees clockwise from 12 o'clock, `r` out from centre.
        /// sin/cos are swapped versus the usual convention precisely because
        /// angle 0 must mean "up", not "right".
        private static Vector2 PointAt(float angle, float r)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad) * r, Mathf.Cos(rad) * r);
        }
    }
}
