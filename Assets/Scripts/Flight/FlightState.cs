using UnityEngine;

namespace Flusi
{
    /// Kinematic aircraft state. Angles in degrees; Speed in metres/second.
    /// Heading is clockwise-from-north yaw; Pitch is nose-up-positive;
    /// Bank is right-roll-positive.
    public struct FlightState
    {
        public Vector3 Position;
        public float Heading;
        public float Pitch;
        public float Bank;
        public float Speed;

        // Unity Euler: +X pitches nose down, +Z rolls left, so negate Pitch/Bank.
        public Quaternion Orientation => Quaternion.Euler(-Pitch, Heading, -Bank);
    }
}
