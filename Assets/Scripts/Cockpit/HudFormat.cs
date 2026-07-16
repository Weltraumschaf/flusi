using UnityEngine;

namespace Flusi
{
    /// Strings for the panel's digital readouts.
    public static class HudFormat
    {
        public static string Altitude(float metres) => $"{Mathf.RoundToInt(metres)} m";

        public static string Speed(float metresPerSecond)
            => $"{Mathf.RoundToInt(FlightDerivations.SpeedKmh(metresPerSecond))} km/h";
    }
}
