using UnityEngine;

namespace Flusi
{
    /// Rotating compass card under a fixed lubber line at the top.
    /// Replaces HeadingCompass.
    public class HeadingIndicator : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private RectTransform card;

        private IAircraftState _state;
        public void SetSource(IAircraftState s) => _state = s;

        private void Awake()
        {
            if (_state == null && aircraftSource != null)
                _state = (IAircraftState)aircraftSource;
        }

        private void Update()
        {
            if (_state == null || card == null) return;

            // The card turns opposite the aircraft so the current heading stays
            // under the lubber line. Heading 90 (east) must bring the card's "E"
            // — which sits 90 degrees clockwise on the face — up to 12 o'clock,
            // so the card rotates counter-clockwise by the heading: +Z in Unity.
            card.localEulerAngles = new Vector3(0f, 0f, _state.HeadingDegrees);
        }
    }
}
