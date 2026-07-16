using UnityEngine;

namespace Flusi
{
    /// Maps world XZ (world centred on origin) to a 0..1 minimap coordinate.
    public static class MinimapProjection
    {
        public static Vector2 WorldToNormalized(Vector2 worldXZ, float worldSizeMetres)
        {
            float half = worldSizeMetres * 0.5f;
            float nx = Mathf.Clamp01((worldXZ.x + half) / worldSizeMetres);
            float ny = Mathf.Clamp01((worldXZ.y + half) / worldSizeMetres);
            return new Vector2(nx, ny);
        }
    }
}
