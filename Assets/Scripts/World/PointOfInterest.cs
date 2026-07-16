using UnityEngine;

namespace Flusi
{
    /// Base for anything shown on the minimap. Self-registers while enabled.
    public abstract class PointOfInterest : MonoBehaviour
    {
        [SerializeField] private string label = "";
        public string Label => label;
        public abstract PoiKind Kind { get; }

        protected virtual void OnEnable() => PointOfInterestRegistry.Register(this);
        protected virtual void OnDisable() => PointOfInterestRegistry.Unregister(this);
    }
}
