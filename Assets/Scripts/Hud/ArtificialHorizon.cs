using UnityEngine;

namespace Flusi
{
    /// Rolls and slides a backing image to indicate bank and pitch.
    public class ArtificialHorizon : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // IAircraftState
        [SerializeField] private RectTransform horizonImage;
        [SerializeField] private float pixelsPerPitchDegree = 3f;

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake() { if (_state == null && aircraftSource != null) _state = (IAircraftState)aircraftSource; }

        private void Update()
        {
            if (_state == null || horizonImage == null) return;
            // Bank rolls the card the opposite way; pitch slides it vertically.
            horizonImage.localEulerAngles = new Vector3(0f, 0f, _state.BankDegrees);
            horizonImage.anchoredPosition = new Vector2(0f, -_state.PitchDegrees * pixelsPerPitchDegree);
        }
    }
}
