using UnityEngine;

namespace Flusi
{
    /// Drives the aircraft: reads input, steps the pure FlightModel each fixed
    /// tick, and applies the result to this transform. Exposes IAircraftState.
    public class AircraftController : MonoBehaviour, IAircraftState
    {
        [SerializeField] private FlightConfig config = FlightConfig.Default;
        [SerializeField] private float startSpeed = 80f;
        [SerializeField] private bool autoLevelOn = true;
        [SerializeField] private bool gearDown = true;

        private FlightControls _controls;
        private FlightState _state;

        public FlightConfig Config { get => config; set => config = value; }

        // IAircraftState
        public float AltitudeMeters => _state.Position.y;
        public float SpeedMetersPerSecond => _state.Speed;
        public float HeadingDegrees => _state.Heading;
        public float PitchDegrees => _state.Pitch;
        public float BankDegrees => _state.Bank;
        public Vector3 WorldPosition => _state.Position;
        public bool AutoLevelOn => autoLevelOn;
        public bool GearDown => gearDown;

        private void Awake()
        {
            _controls = new FlightControls();
            _state = new FlightState
            {
                Position = transform.position,
                Heading = transform.eulerAngles.y,
                Pitch = 0f, Bank = 0f, Speed = startSpeed
            };
        }

        private void OnEnable()
        {
            _controls.Enable();
            _controls.Flight.ToggleAutoLevel.performed += OnToggleAutoLevel;
            _controls.Flight.ToggleGear.performed += OnToggleGear;
        }

        private void OnDisable()
        {
            _controls.Flight.ToggleAutoLevel.performed -= OnToggleAutoLevel;
            _controls.Flight.ToggleGear.performed -= OnToggleGear;
            _controls.Disable();
        }

        private void OnDestroy() => _controls?.Dispose();

        private void OnToggleAutoLevel(UnityEngine.InputSystem.InputAction.CallbackContext _)
            => autoLevelOn = !autoLevelOn;

        /// Public so tests and future cockpit switches can toggle the gear
        /// without synthesising keyboard input.
        public void ToggleGear() => gearDown = !gearDown;

        private void OnToggleGear(UnityEngine.InputSystem.InputAction.CallbackContext _)
            => ToggleGear();

        private void FixedUpdate()
        {
            var input = new FlightInput
            {
                Pitch = _controls.Flight.Pitch.ReadValue<float>(),
                Turn = _controls.Flight.Turn.ReadValue<float>(),
                Throttle = _controls.Flight.Throttle.ReadValue<float>(),
                AutoLevel = autoLevelOn
            };

            _state = FlightModel.Step(_state, input, SampleGroundHeight(_state.Position),
                                      config, Time.fixedDeltaTime);

            transform.SetPositionAndRotation(_state.Position, _state.Orientation);
        }

        private static float SampleGroundHeight(Vector3 worldPos)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return 0f;
            return terrain.SampleHeight(worldPos) + terrain.transform.position.y;
        }
    }
}
