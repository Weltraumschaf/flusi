namespace Flusi
{
    /// Per-frame pilot intent. All axes are normalized to -1..1.
    public struct FlightInput
    {
        public float Pitch;    // -1 = nose down, +1 = nose up
        public float Turn;     // -1 = left,      +1 = right
        public float Throttle; // -1 = slower,    +1 = faster
        public bool AutoLevel; // true = self-levelling assist enabled
    }
}
