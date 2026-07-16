using UnityEngine;

namespace Flusi
{
    /// A single-needle round gauge. One component, several instances; the
    /// channel decides what it reads. Every calibration value is serialized so
    /// it can be tuned in the Inspector while the game runs.
    public class NeedleGauge : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private RectTransform needle;
        [SerializeField] private GaugeChannel channel = GaugeChannel.AirspeedKmh;

        [Header("Calibration (see spec 3.2)")]
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 500f;
        [SerializeField] private float startAngle = 0f;
        [SerializeField] private float sweepAngle = 320f;

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake()
        {
            if (_state == null && aircraftSource != null)
                _state = (IAircraftState)aircraftSource;
        }

        private void Update()
        {
            if (_state == null || needle == null) return;

            float angle = GaugeScale.ValueToAngle(Read(_state), minValue, maxValue,
                                                  startAngle, sweepAngle);
            needle.localEulerAngles = new Vector3(0f, 0f, -angle);
        }

        /// Not static: it reads the instance field `channel`.
        private float Read(IAircraftState s) => channel switch
        {
            GaugeChannel.AirspeedKmh => FlightDerivations.SpeedKmh(s.SpeedMetersPerSecond),
            GaugeChannel.VerticalSpeed => FlightDerivations.VerticalSpeed(
                                              s.SpeedMetersPerSecond, s.PitchDegrees),
            GaugeChannel.BankDegrees => s.BankDegrees,
            _ => 0f,
        };
    }
}
