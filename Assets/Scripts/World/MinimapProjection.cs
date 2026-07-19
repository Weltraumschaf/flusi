using UnityEngine;

namespace Flusi
{
    /// Maps world XZ to a 0..1 minimap coordinate, independently per axis,
    /// so a rectangular (non-square) world maps correctly.
    public static class MinimapProjection
    {
        public static Vector2 WorldToNormalized(Vector2 worldXZ, Vector2 worldMin, Vector2 worldMax)
        {
            float nx = Mathf.Clamp01((worldXZ.x - worldMin.x) / (worldMax.x - worldMin.x));
            float ny = Mathf.Clamp01((worldXZ.y - worldMin.y) / (worldMax.y - worldMin.y));
            return new Vector2(nx, ny);
        }
    }
}
