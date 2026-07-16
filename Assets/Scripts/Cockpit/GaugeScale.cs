using UnityEngine;

namespace Flusi
{
    /// Pure needle maths for round gauges.
    ///
    /// Angle convention: degrees, 0 = needle points at 12 o'clock, positive =
    /// clockwise. Components apply localEulerAngles.z = -angle, because Unity's
    /// Z rotation runs counter-clockwise.
    public static class GaugeScale
    {
        /// Maps value onto a needle angle, clamped at both ends of the range.
        /// A negative sweepAngle runs the gauge counter-clockwise.
        public static float ValueToAngle(float value, float minValue, float maxValue,
                                         float startAngle, float sweepAngle)
        {
            float t = Mathf.InverseLerp(minValue, maxValue, value);
            return startAngle + t * sweepAngle;
        }
    }
}
