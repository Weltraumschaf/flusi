using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flusi
{
    /// Pure land/water maths for the minimap background: given a terrain
    /// height sampler, a sea-level threshold, and river boundary polygons,
    /// produces a flat 2-tone Color32 buffer. No Unity scene dependency.
    public static class MinimapTerrainTexture
    {
        public static readonly Color32 LandColor = new Color32(72, 148, 72, 255);
        public static readonly Color32 WaterColor = new Color32(58, 110, 189, 255);

        public static Color32[] Build(
            int width,
            int height,
            Func<float, float, float> terrainHeightAt,
            float seaLevel,
            IReadOnlyList<Vector2[]> riverPolygons,
            Vector2 worldMin,
            Vector2 worldMax)
        {
            var pixels = new Color32[width * height];
            float worldW = worldMax.x - worldMin.x;
            float worldH = worldMax.y - worldMin.y;

            for (int py = 0; py < height; py++)
            {
                float v = (py + 0.5f) / height;
                float worldZ = worldMin.y + v * worldH;

                for (int px = 0; px < width; px++)
                {
                    float u = (px + 0.5f) / width;
                    float worldX = worldMin.x + u * worldW;

                    bool isWater = terrainHeightAt(worldX, worldZ) <= seaLevel;
                    if (!isWater)
                    {
                        var point = new Vector2(worldX, worldZ);
                        for (int i = 0; i < riverPolygons.Count && !isWater; i++)
                            isWater = InsidePolygon(point, riverPolygons[i]);
                    }

                    pixels[py * width + px] = isWater ? WaterColor : LandColor;
                }
            }

            return pixels;
        }

        /// Standard even-odd ray-casting point-in-polygon test.
        public static bool InsidePolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            int j = polygon.Length - 1;
            for (int i = 0; i < polygon.Length; i++)
            {
                bool crosses = (polygon[i].y > point.y) != (polygon[j].y > point.y);
                if (crosses)
                {
                    float xAtY = (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y)
                        / (polygon[j].y - polygon[i].y) + polygon[i].x;
                    if (point.x < xAtY) inside = !inside;
                }
                j = i;
            }
            return inside;
        }

        /// Size of the largest box with `aspectWidthOverHeight` that fits
        /// centred inside `containerSize`.
        public static Vector2 FitWithLetterbox(Vector2 containerSize, float aspectWidthOverHeight)
        {
            float containerAspect = containerSize.x / containerSize.y;
            if (containerAspect > aspectWidthOverHeight)
            {
                float h = containerSize.y;
                return new Vector2(h * aspectWidthOverHeight, h);
            }
            else
            {
                float w = containerSize.x;
                return new Vector2(w, w / aspectWidthOverHeight);
            }
        }
    }
}
