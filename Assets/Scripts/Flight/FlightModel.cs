using UnityEngine;

namespace Flusi
{
    /// Pure kinematic arcade flight. No Rigidbody, no physics forces.
    /// Deterministic: given the same inputs it always produces the same state.
    public static class FlightModel
    {
        public static FlightState Step(FlightState state, FlightInput input,
                                       float groundHeight, FlightConfig cfg, float dt)
        {
            // Speed: throttle accelerates within a clamped band; never stalls.
            state.Speed = Mathf.Clamp(
                state.Speed + input.Throttle * cfg.ThrottleAccel * dt,
                cfg.MinSpeed, cfg.MaxSpeed);

            return state;
        }
    }
}
