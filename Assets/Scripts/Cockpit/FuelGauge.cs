using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// Fuel quantity bar.
    ///
    /// PLACEHOLDER — this is the one instrument on the panel that does not
    /// report anything real. `level` is a static serialized value; there is no
    /// fuel burn in the game, so the needle never moves however far you fly.
    /// This is deliberate and owner-approved, not an oversight: see
    /// docs/superpowers/specs/2026-07-16-cockpit-instruments-design.md 3.5,
    /// which also records why draining fuel is blocked (Specification.md 2
    /// rules out fail states) and the two routes for wiring it up later.
    ///
    /// To make it live, replace the `level` read in Update with a state read.
    /// Nothing else here changes.
    public class FuelGauge : MonoBehaviour
    {
        /// Requires an Image with type = Filled, so fillAmount does something.
        [SerializeField] private Image fillBar;
        [SerializeField, Range(0f, 1f)] private float level = 1f;

        public float Level
        {
            get => level;
            set => level = Mathf.Clamp01(value);
        }

        private void Update()
        {
            if (fillBar != null) fillBar.fillAmount = level;
        }
    }
}
