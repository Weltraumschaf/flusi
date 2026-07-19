using System;
using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class MinimapTerrainTextureTests
    {
        private static readonly Vector2[] UnitSquare =
        {
            new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(10f, 10f), new Vector2(10f, 0f)
        };

        [Test]
        public void InsidePolygon_PointInside_ReturnsTrue()
        {
            Assert.IsTrue(MinimapTerrainTexture.InsidePolygon(new Vector2(5f, 5f), UnitSquare));
        }

        [Test]
        public void InsidePolygon_PointOutside_ReturnsFalse()
        {
            Assert.IsFalse(MinimapTerrainTexture.InsidePolygon(new Vector2(20f, 20f), UnitSquare));
        }

        [Test]
        public void FitWithLetterbox_SquareContainer_WidthConstrained()
        {
            var size = MinimapTerrainTexture.FitWithLetterbox(new Vector2(200f, 200f), 22000f / 12000f);
            Assert.AreEqual(200f, size.x, 0.01f);
            Assert.AreEqual(200f * 12000f / 22000f, size.y, 0.01f);
        }

        [Test]
        public void FitWithLetterbox_WideContainer_HeightConstrained()
        {
            var size = MinimapTerrainTexture.FitWithLetterbox(new Vector2(400f, 100f), 22000f / 12000f);
            Assert.AreEqual(100f, size.y, 0.01f);
            Assert.AreEqual(100f * 22000f / 12000f, size.x, 0.01f);
        }

        [Test]
        public void Build_AboveSeaLevelEverywhere_AllLand()
        {
            var pixels = MinimapTerrainTexture.Build(
                2, 2,
                (x, z) => 100f,
                seaLevel: 5f,
                riverPolygons: Array.Empty<Vector2[]>(),
                worldMin: new Vector2(-10f, -10f),
                worldMax: new Vector2(10f, 10f));

            Assert.AreEqual(4, pixels.Length);
            foreach (var p in pixels)
                Assert.AreEqual(MinimapTerrainTexture.LandColor, p);
        }

        [Test]
        public void Build_BelowSeaLevelEverywhere_AllWater()
        {
            var pixels = MinimapTerrainTexture.Build(
                2, 2,
                (x, z) => 0f,
                seaLevel: 5f,
                riverPolygons: Array.Empty<Vector2[]>(),
                worldMin: new Vector2(-10f, -10f),
                worldMax: new Vector2(10f, 10f));

            foreach (var p in pixels)
                Assert.AreEqual(MinimapTerrainTexture.WaterColor, p);
        }

        [Test]
        public void Build_RiverPolygonOverridesHighLand()
        {
            // Covers the whole sampled area, so every pixel should read as
            // water even though the height function says "land".
            var river = new[]
            {
                new Vector2(-10f, -10f), new Vector2(-10f, 10f),
                new Vector2(10f, 10f), new Vector2(10f, -10f)
            };
            var pixels = MinimapTerrainTexture.Build(
                2, 2,
                (x, z) => 100f,
                seaLevel: 5f,
                riverPolygons: new[] { river },
                worldMin: new Vector2(-10f, -10f),
                worldMax: new Vector2(10f, 10f));

            foreach (var p in pixels)
                Assert.AreEqual(MinimapTerrainTexture.WaterColor, p);
        }
    }
}
