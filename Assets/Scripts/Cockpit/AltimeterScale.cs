using UnityEngine;

namespace Flusi
{
    /// Pure two-needle altimeter maths.
    /// Long needle: one revolution per 1000 m. Short needle: one per 10 000 m.
    ///
    /// Angle convention as GaugeScale: degrees clockwise from 12 o'clock.
    public static class AltimeterScale
    {
        public static float HundredsAngle(float altitudeMetres)
            => Mathf.Repeat(altitudeMetres, 1000f) / 1000f * 360f;

        public static float ThousandsAngle(float altitudeMetres)
            => Mathf.Repeat(altitudeMetres, 10000f) / 10000f * 360f;
    }
}
