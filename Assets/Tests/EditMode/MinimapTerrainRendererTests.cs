using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class MinimapTerrainRendererTests
    {
        [Test]
        public void RiverPolygonFromMesh_WalksLeftBankThenRightBankReversed()
        {
            // Simulate a 2-station ribbon: (left0,right0), (left1,right1).
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),   // left0  (index 0)
                    new Vector3(1f, 0f, 0f),   // right0 (index 1)
                    new Vector3(0f, 0f, 10f),  // left1  (index 2)
                    new Vector3(1f, 0f, 10f),  // right1 (index 3)
                }
            };

            var go = new GameObject("TestRiver", typeof(MeshFilter));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var polygon = MinimapTerrainRenderer.RiverPolygonFromMesh(go.GetComponent<MeshFilter>());

            // Expected walk: left0, left1 (forward), then right1, right0 (backward).
            Assert.AreEqual(4, polygon.Length);
            Assert.AreEqual(new Vector2(0f, 0f), polygon[0]);
            Assert.AreEqual(new Vector2(0f, 10f), polygon[1]);
            Assert.AreEqual(new Vector2(1f, 10f), polygon[2]);
            Assert.AreEqual(new Vector2(1f, 0f), polygon[3]);

            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(go);
        }
    }
}
