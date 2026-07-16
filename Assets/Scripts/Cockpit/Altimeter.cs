using UnityEngine;

namespace Flusi
{
    /// Two-needle altimeter: the long needle turns once per 1000 m, the short
    /// needle once per 10 000 m. Kept separate from NeedleGauge because it drives
    /// two needles from one value and wraps rather than clamping.
    public class Altimeter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private RectTransform hundredsNeedle;
        [SerializeField] private RectTransform thousandsNeedle;

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake()
        {
            if (_state == null && aircraftSource != null)
                _state = (IAircraftState)aircraftSource;
        }

        private void Update()
        {
            if (!AircraftStateRef.IsAlive(_state)) return;

            float altitude = _state.AltitudeMeters;

            if (hundredsNeedle != null)
                hundredsNeedle.localEulerAngles =
                    new Vector3(0f, 0f, -AltimeterScale.HundredsAngle(altitude));

            if (thousandsNeedle != null)
                thousandsNeedle.localEulerAngles =
                    new Vector3(0f, 0f, -AltimeterScale.ThousandsAngle(altitude));
        }
    }
}
