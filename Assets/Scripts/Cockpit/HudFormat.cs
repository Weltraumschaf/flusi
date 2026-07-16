using UnityEngine;

namespace Flusi
{
    /// Strings for the panel's digital readouts.
    public static class HudFormat
    {
        public static string Altitude(float metres) => $"{Mathf.RoundToInt(metres)} m";

        public static string Speed(float metresPerSecond)
            => $"{Mathf.RoundToInt(FlightDerivations.SpeedKmh(metresPerSecond))} km/h";

        private static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// Dead once HudController goes — the rotating HeadingIndicator supersedes
        /// it. Removed together with its only caller in Task 9; deleting it any
        /// earlier breaks the build for every task in between.
        public static string Compass(float headingDeg)
        {
            float h = Mathf.Repeat(headingDeg, 360f);
            int idx = Mathf.RoundToInt(h / 45f) % 8;
            return Points[idx];
        }
    }
}
