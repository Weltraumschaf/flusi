using UnityEngine;
using UnityEngine.InputSystem;

namespace Flusi
{
    /// Owns the two cameras and toggles between them on ToggleView.
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera cockpitCamera;
        [SerializeField] private Camera orbitCamera;
        [SerializeField] private Transform aircraft;
        [SerializeField] private Canvas hudCanvas; // Screen Space - Camera; must follow whichever camera is enabled

        public ViewMode Current { get; private set; } = ViewMode.Cockpit;
        public event System.Action<ViewMode> ViewChanged;

        private FlightControls _controls;

        private void Awake()
        {
            _controls = new FlightControls();
            if (cockpitCamera == null || orbitCamera == null)
            {
                Debug.LogError("CameraRig: cockpitCamera and orbitCamera must be assigned.", this);
                return;
            }
            var orbit = orbitCamera.GetComponent<OrbitCamera>();
            if (orbit != null) orbit.SetTarget(aircraft);
            Apply();
        }

        private void OnEnable()
        {
            _controls.Enable();
            _controls.Flight.ToggleView.performed += OnToggleView;
        }

        private void OnDisable()
        {
            _controls.Flight.ToggleView.performed -= OnToggleView;
            _controls.Disable();
        }

        private void OnDestroy() => _controls?.Dispose();

        /// Public so tests and future cockpit switches can change view without
        /// synthesising keyboard input. Mirrors AircraftController.ToggleGear.
        public void ToggleView()
        {
            Current = Current == ViewMode.Cockpit ? ViewMode.Orbit : ViewMode.Cockpit;
            Apply();
            ViewChanged?.Invoke(Current);
        }

        private void OnToggleView(InputAction.CallbackContext _) => ToggleView();

        private void Apply()
        {
            if (cockpitCamera == null || orbitCamera == null) return;
            bool cockpit = Current == ViewMode.Cockpit;
            cockpitCamera.enabled = cockpit;
            orbitCamera.enabled = !cockpit;
            if (hudCanvas != null)
                hudCanvas.worldCamera = cockpit ? cockpitCamera : orbitCamera;
        }
    }
}
