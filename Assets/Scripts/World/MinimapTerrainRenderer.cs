using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Flusi
{
    /// Bakes the minimap's land/water background once, shortly after start,
    /// from the live terrain and river meshes. Static bake — the world
    /// doesn't change at runtime, so this never re-runs after its first pass.
    public class MinimapTerrainRenderer : MonoBehaviour
    {
        private const int TextureWidth = 256;
        private const int TextureHeight = 140; // ~22000:12000 world aspect

        [SerializeField] private Minimap minimap;      // source of truth for world extent
        [SerializeField] private Terrain terrain;
        [SerializeField] private MeshFilter[] riverMeshes;
        [SerializeField] private float seaLevel = 5f;  // matches the Sea mesh's world Y
        [SerializeField] private RectTransform panel;  // the square minimap area to fit inside
        [SerializeField] private Image background;     // this component's own Image

        private void Awake()
        {
            var riverPolygons = new List<Vector2[]>(riverMeshes.Length);
            foreach (var mf in riverMeshes)
                if (mf != null) riverPolygons.Add(RiverPolygonFromMesh(mf));

            var pixels = MinimapTerrainTexture.Build(
                TextureWidth, TextureHeight,
                SampleTerrainHeight,
                seaLevel,
                riverPolygons,
                minimap.WorldMin, minimap.WorldMax);

            var texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            background.sprite = Sprite.Create(
                texture, new Rect(0f, 0f, TextureWidth, TextureHeight), new Vector2(0.5f, 0.5f));
        }

        private const int MaxSizeStabilizeFrames = 30; // safety cap (~0.5s at 60fps), never expected to be hit

        // panel.rect.size is not reliable at Awake, and a fixed one-frame
        // wait was NOT enough in every environment (confirmed: fine in the
        // Editor, still stale in a standalone build) - the Canvas's real
        // pixel dimensions settle at a point that varies by platform/build,
        // not a fixed frame count. Wait until the read stops changing
        // between frames instead of guessing how many frames it takes.
        private IEnumerator Start()
        {
            Vector2 previousSize = panel.rect.size;
            yield return null;
            int framesWaited = 0;
            while (panel.rect.size != previousSize && framesWaited < MaxSizeStabilizeFrames)
            {
                previousSize = panel.rect.size;
                yield return null;
                framesWaited++;
            }

            float worldAspect = (minimap.WorldMax.x - minimap.WorldMin.x)
                / (minimap.WorldMax.y - minimap.WorldMin.y);
            var fitSize = MinimapTerrainTexture.FitWithLetterbox(panel.rect.size, worldAspect);

            var rt = background.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = fitSize;
        }

        private float SampleTerrainHeight(float worldX, float worldZ) =>
            terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));

        /// Walks a river ribbon mesh's vertices (ordered left/right-bank
        /// pairs per station) into one closed 2D boundary polygon: left
        /// bank forward, then right bank backward.
        public static Vector2[] RiverPolygonFromMesh(MeshFilter mf)
        {
            var verts = mf.sharedMesh.vertices;
            int pairs = verts.Length / 2;
            var polygon = new Vector2[verts.Length];

            for (int i = 0; i < pairs; i++)
            {
                var world = mf.transform.TransformPoint(verts[i * 2]);
                polygon[i] = new Vector2(world.x, world.z);
            }
            for (int i = 0; i < pairs; i++)
            {
                var world = mf.transform.TransformPoint(verts[verts.Length - 1 - i * 2]);
                polygon[pairs + i] = new Vector2(world.x, world.z);
            }

            return polygon;
        }
    }
}
