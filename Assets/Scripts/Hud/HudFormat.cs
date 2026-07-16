using UnityEngine;

namespace Flusi
{
    public static class HudFormat
    {
        public static string Altitude(float metres) => $"{Mathf.RoundToInt(metres)} m";

        public static string Speed(float metresPerSecond)
            => $"{Mathf.RoundToInt(metresPerSecond * 3.6f)} km/h";

        private static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        public static string Compass(float headingDeg)
        {
            float h = Mathf.Repeat(headingDeg, 360f);
            int idx = Mathf.RoundToInt(h / 45f) % 8;
            return Points[idx];
        }
    }
}
