namespace Flusi
{
    /// Tunable arcade-flight parameters. Serializable so it shows in the Inspector.
    [System.Serializable]
    public struct FlightConfig
    {
        public float MinSpeed;             // m/s; plane never goes below this (always glides)
        public float MaxSpeed;             // m/s
        public float ThrottleAccel;        // m/s^2 at full throttle input
        public float PitchRateDeg;         // deg/s pitch change at full input
        public float MaxPitchDeg;          // clamp for pitch magnitude
        public float MaxBankDeg;           // bank at full turn input
        public float BankRateDeg;          // deg/s the bank moves toward its target
        public float TurnRateDegAtMaxBank; // deg/s heading change when banked fully
        public float AutoLevelStrength;    // return rate; eases MaxAngle * this degrees/sec toward level
        public float GroundClearance;      // metres kept above terrain

        public static FlightConfig Default => new FlightConfig
        {
            MinSpeed = 40f,
            MaxSpeed = 130f,
            ThrottleAccel = 25f,
            PitchRateDeg = 40f,
            MaxPitchDeg = 45f,
            MaxBankDeg = 55f,
            BankRateDeg = 90f,
            TurnRateDegAtMaxBank = 35f,
            AutoLevelStrength = 2f,
            GroundClearance = 8f
        };
    }
}
