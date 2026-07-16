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

            // Bank: integrate from turn input and hold on release; the
            // auto-level term below is the only thing that returns it to level.
            state.Bank = Mathf.Clamp(
                state.Bank + input.Turn * cfg.BankRateDeg * dt,
                -cfg.MaxBankDeg, cfg.MaxBankDeg);

            // Pitch: integrate input, clamp magnitude.
            state.Pitch = Mathf.Clamp(
                state.Pitch + input.Pitch * cfg.PitchRateDeg * dt,
                -cfg.MaxPitchDeg, cfg.MaxPitchDeg);

            // Auto-level assist: on an idle axis, ease the angle back to level.
            if (input.AutoLevel)
            {
                if (Mathf.Approximately(input.Turn, 0f))
                    state.Bank = Mathf.MoveTowards(
                        state.Bank, 0f, cfg.AutoLevelStrength * cfg.MaxBankDeg * dt);
                if (Mathf.Approximately(input.Pitch, 0f))
                    state.Pitch = Mathf.MoveTowards(
                        state.Pitch, 0f, cfg.AutoLevelStrength * cfg.MaxPitchDeg * dt);
            }

            // Coordinated turn: heading changes in proportion to current bank.
            state.Heading = Mathf.Repeat(
                state.Heading + (state.Bank / cfg.MaxBankDeg) * cfg.TurnRateDegAtMaxBank * dt,
                360f);

            // Integrate position along the forward vector (heading + pitch).
            Vector3 forward = Quaternion.Euler(-state.Pitch, state.Heading, 0f) * Vector3.forward;
            state.Position += forward * state.Speed * dt;

            // Soft ground contact: never crash. Skim along the clearance floor
            // and cancel any downward pitch so the plane levels off.
            float floor = groundHeight + cfg.GroundClearance;
            if (state.Position.y < floor)
            {
                state.Position.y = floor;
                if (state.Pitch < 0f) state.Pitch = 0f;
            }

            return state;
        }
    }
}
