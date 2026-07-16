using UnityEngine;

namespace Flusi
{
    /// Values derived from IAircraftState that no gauge should recompute itself.
    public static class FlightDerivations
    {
        /// Metres per second of climb. This is exact, not an approximation:
        /// FlightModel integrates position along
        /// Quaternion.Euler(-Pitch, Heading, 0) * Vector3.forward, whose Y
        /// component is sin(pitch).
        public static float VerticalSpeed(float speedMetresPerSecond, float pitchDegrees)
            => speedMetresPerSecond * Mathf.Sin(pitchDegrees * Mathf.Deg2Rad);

        public static float SpeedKmh(float metresPerSecond) => metresPerSecond * 3.6f;
    }
}
