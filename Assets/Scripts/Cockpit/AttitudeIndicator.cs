using UnityEngine;

namespace Flusi
{
    /// Artificial horizon. bankRoot rolls with bank; pitchCard, its child,
    /// slides with pitch inside that rolled frame. Both sit behind a circular
    /// mask, under a fixed aircraft symbol.
    ///
    /// Replaces ArtificialHorizon.
    public class AttitudeIndicator : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private RectTransform bankRoot;
        [SerializeField] private RectTransform pitchCard;
        [SerializeField] private RectTransform rollPointer;
        [SerializeField] private float pixelsPerPitchDegree = 1.5f;

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

            // The horizon rolls opposite the aircraft, so a right bank tips the
            // horizon line left. Unity's positive Z is counter-clockwise, which
            // is already the direction we want here.
            if (bankRoot != null)
                bankRoot.localEulerAngles = new Vector3(0f, 0f, _state.BankDegrees);

            if (pitchCard != null)
                pitchCard.anchoredPosition =
                    new Vector2(0f, -_state.PitchDegrees * pixelsPerPitchDegree);

            if (rollPointer != null)
                rollPointer.localEulerAngles = new Vector3(0f, 0f, _state.BankDegrees);
        }
    }
}
