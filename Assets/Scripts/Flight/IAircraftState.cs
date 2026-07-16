using UnityEngine;

namespace Flusi
{
    /// Read-only view of the aircraft for HUD, minimap and cameras.
    /// Consumers must depend on this, never on FlightModel/AircraftController internals.
    public interface IAircraftState
    {
        float AltitudeMeters { get; }
        float SpeedMetersPerSecond { get; }
        float HeadingDegrees { get; }
        float PitchDegrees { get; }
        float BankDegrees { get; }
        Vector3 WorldPosition { get; }
        bool AutoLevelOn { get; }
    }
}
