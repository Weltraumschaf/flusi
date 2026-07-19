using System.Collections.Generic;
using UnityEngine;

namespace Flusi
{
    /// Draws the plane blip and one marker per registered POI on a UI panel.
    public class Minimap : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour aircraftSource; // must implement IAircraftState
        [SerializeField] private RectTransform panel;          // the map area
        [SerializeField] private RectTransform planeBlip;
        [SerializeField] private RectTransform airportMarkerPrefab;
        [SerializeField] private RectTransform landmarkMarkerPrefab;
        [SerializeField] private Vector2 worldMin = new Vector2(-11000f, -6000f);
        [SerializeField] private Vector2 worldMax = new Vector2(11000f, 6000f);

        public Vector2 WorldMin => worldMin;
        public Vector2 WorldMax => worldMax;

        private IAircraftState _state;
        private readonly List<(RectTransform marker, Vector2 worldXZ)> _markers = new();

        private void Awake()
        {
            _state = aircraftSource as IAircraftState;
            if (_state == null)
                Debug.LogError("Minimap: aircraftSource must implement IAircraftState.", this);
        }

        private void OnEnable() => RebuildMarkers();

        private void RebuildMarkers()
        {
            foreach (var m in _markers)
                if (m.marker != null) Destroy(m.marker.gameObject);
            _markers.Clear();
            if (panel == null) return;
            foreach (var poi in PointOfInterestRegistry.All)
            {
                var prefab = poi.Kind == PoiKind.Airport ? airportMarkerPrefab : landmarkMarkerPrefab;
                if (prefab == null) continue;
                var marker = Instantiate(prefab, panel);
                var worldXZ = new Vector2(poi.transform.position.x, poi.transform.position.z);
                _markers.Add((marker, worldXZ));
            }
        }

        private void Update()
        {
            if (panel == null) return;
            if (_markers.Count != PointOfInterestRegistry.All.Count) RebuildMarkers();
            if (AircraftStateRef.IsAlive(_state) && planeBlip != null)
                Place(planeBlip, new Vector2(_state.WorldPosition.x, _state.WorldPosition.z));
            foreach (var m in _markers)
                if (m.marker != null) Place(m.marker, m.worldXZ);
        }

        private void Place(RectTransform marker, Vector2 worldXZ)
        {
            Vector2 n = MinimapProjection.WorldToNormalized(worldXZ, worldMin, worldMax);
            Vector2 size = panel.rect.size;
            marker.anchoredPosition = new Vector2((n.x - 0.5f) * size.x, (n.y - 0.5f) * size.y);
        }
    }
}
