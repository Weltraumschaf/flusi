using UnityEngine;

namespace Flusi
{
    /// External camera that tracks a target's position while the player
    /// rotates the viewing angle around it via the OrbitLook axis.
    public class OrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 40f;
        [SerializeField] private float height = 12f;
        [SerializeField] private float yawSpeedDeg = 60f;

        private FlightControls _controls;
        private float _yaw;

        public void SetTarget(Transform t) => target = t;

        private void Awake() => _controls = new FlightControls();
        private void OnEnable() => _controls.Enable();
        private void OnDisable() => _controls.Disable();
        private void OnDestroy() => _controls?.Dispose();

        private void LateUpdate()
        {
            if (target == null) return;
            _yaw += _controls.Flight.OrbitLook.ReadValue<float>() * yawSpeedDeg * Time.deltaTime;
            Quaternion rot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 offset = rot * new Vector3(0f, height, -distance);
            transform.position = target.position + offset;
            transform.LookAt(target.position + Vector3.up * height * 0.25f);
        }
    }
}
