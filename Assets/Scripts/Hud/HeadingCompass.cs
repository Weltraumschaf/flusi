using UnityEngine;

namespace Flusi
{
    /// Rotates a compass rose so the current heading sits under the top marker.
    public class HeadingCompass : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // IAircraftState
        [SerializeField] private RectTransform rose;

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake() { if (_state == null && aircraftSource != null) _state = (IAircraftState)aircraftSource; }

        private void Update()
        {
            if (_state == null || rose == null) return;
            rose.localEulerAngles = new Vector3(0f, 0f, _state.HeadingDegrees);
        }
    }
}
