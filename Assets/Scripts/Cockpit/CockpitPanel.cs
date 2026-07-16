using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// Owns the cockpit instrument panel: shows it in cockpit view, hides it in
    /// orbit view, and keeps the digital readouts fed.
    ///
    /// The gauges feed themselves from IAircraftState; this only handles what
    /// they cannot — panel visibility and the plain-number readouts.
    ///
    /// Replaces HudController.
    public class CockpitPanel : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private GameObject panelRoot;

        [Header("Digital readouts (spec 2.1)")]
        [SerializeField] private Text altitudeText;
        [SerializeField] private Text speedText;

        private IAircraftState _state;

        private void Awake()
        {
            if (aircraftSource != null) _state = (IAircraftState)aircraftSource;
        }

        private void OnEnable()
        {
            if (cameraRig != null)
            {
                cameraRig.ViewChanged += OnViewChanged;
                OnViewChanged(cameraRig.Current); // sync to whatever view we start in
            }
        }

        private void OnDisable()
        {
            if (cameraRig != null) cameraRig.ViewChanged -= OnViewChanged;
        }

        private void OnViewChanged(ViewMode mode)
        {
            if (panelRoot != null) panelRoot.SetActive(mode == ViewMode.Cockpit);
        }

        private void Update()
        {
            if (!AircraftStateRef.IsAlive(_state) || panelRoot == null || !panelRoot.activeSelf) return;

            if (altitudeText != null)
                altitudeText.text = HudFormat.Altitude(_state.AltitudeMeters);

            if (speedText != null)
                speedText.text = HudFormat.Speed(_state.SpeedMetersPerSecond);
        }
    }
}
