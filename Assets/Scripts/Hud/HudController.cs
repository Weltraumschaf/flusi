using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// Reads IAircraftState each frame and updates the text instruments.
    /// Hidden in Orbit view (Spec §4).
    public class HudController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private Text altitudeText;
        [SerializeField] private Text speedText;
        [SerializeField] private Text compassText;
        [SerializeField] private Text autoLevelText;

        private IAircraftState _state;

        private void Awake() => _state = (IAircraftState)aircraftSource;

        private void OnEnable()
        {
            if (cameraRig != null)
            {
                cameraRig.ViewChanged += OnViewChanged;
                OnViewChanged(cameraRig.Current); // sync initial visibility to current view
            }
        }

        private void OnDisable()
        {
            if (cameraRig != null) cameraRig.ViewChanged -= OnViewChanged;
        }

        private void OnViewChanged(ViewMode mode)
        {
            if (hudRoot != null) hudRoot.SetActive(mode == ViewMode.Cockpit);
        }

        private void Update()
        {
            if (_state == null || hudRoot == null || !hudRoot.activeSelf) return;
            altitudeText.text = HudFormat.Altitude(_state.AltitudeMeters);
            speedText.text = HudFormat.Speed(_state.SpeedMetersPerSecond);
            compassText.text = HudFormat.Compass(_state.HeadingDegrees);
            autoLevelText.text = _state.AutoLevelOn ? "ASSIST: ON" : "ASSIST: OFF";
        }
    }
}
