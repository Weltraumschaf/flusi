using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// A two-state annunciator. Two instances on the panel: ASSIST and GEAR.
    /// Captions and colours are serialized so one component covers both.
    public class AnnunciatorLamp : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private Text caption;
        [SerializeField] private Image lamp;
        [SerializeField] private LampChannel channel = LampChannel.AutoLevel;

        [Header("Captions")]
        [SerializeField] private string onText = "ASSIST ON";
        [SerializeField] private string offText = "ASSIST OFF";

        [Header("Colours")]
        [SerializeField] private Color onColor = new Color(0.20f, 0.90f, 0.30f);
        [SerializeField] private Color offColor = new Color(0.35f, 0.35f, 0.35f);

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

            bool on = channel switch
            {
                LampChannel.AutoLevel => _state.AutoLevelOn,
                LampChannel.GearDown => _state.GearDown,
                _ => false,
            };

            if (caption != null) caption.text = on ? onText : offText;
            if (lamp != null) lamp.color = on ? onColor : offColor;
        }
    }
}
