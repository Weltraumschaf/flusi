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

        private void OnToggleView(InputAction.CallbackContext _)
        {
            Current = Current == ViewMode.Cockpit ? ViewMode.Orbit : ViewMode.Cockpit;
            Apply();
            ViewChanged?.Invoke(Current);
        }

        private void Apply()
        {
            if (cockpitCamera == null || orbitCamera == null) return;
            bool cockpit = Current == ViewMode.Cockpit;
            cockpitCamera.enabled = cockpit;
            orbitCamera.enabled = !cockpit;
        }
    }
}
