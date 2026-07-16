using System.Collections.Generic;

namespace Flusi
{
    public enum PoiKind { Airport, Landmark }

    /// Scene-wide list of points of interest the minimap draws. POIs add and
    /// remove themselves; consumers only read.
    public static class PointOfInterestRegistry
    {
        private static readonly List<PointOfInterest> _items = new List<PointOfInterest>();
        public static IReadOnlyList<PointOfInterest> All => _items;

        public static void Register(PointOfInterest poi)
        {
            if (!_items.Contains(poi)) _items.Add(poi);
        }

        public static void Unregister(PointOfInterest poi) => _items.Remove(poi);

        public static void Clear() => _items.Clear();
    }
}
