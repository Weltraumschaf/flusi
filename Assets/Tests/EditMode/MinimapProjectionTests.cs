using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class MinimapProjectionTests
    {
        private static readonly Vector2 WorldMin = new Vector2(-11000f, -6000f);
        private static readonly Vector2 WorldMax = new Vector2(11000f, 6000f);

        [Test]
        public void Origin_MapsToCentre()
        {
            var n = MinimapProjection.WorldToNormalized(Vector2.zero, WorldMin, WorldMax);
            Assert.AreEqual(0.5f, n.x, 0.0001f);
            Assert.AreEqual(0.5f, n.y, 0.0001f);
        }

        [Test]
        public void PositiveCorner_MapsToOne()
        {
            var n = MinimapProjection.WorldToNormalized(WorldMax, WorldMin, WorldMax);
            Assert.AreEqual(1f, n.x, 0.0001f);
            Assert.AreEqual(1f, n.y, 0.0001f);
        }

        [Test]
        public void NegativeCorner_MapsToZero()
        {
            var n = MinimapProjection.WorldToNormalized(WorldMin, WorldMin, WorldMax);
            Assert.AreEqual(0f, n.x, 0.0001f);
            Assert.AreEqual(0f, n.y, 0.0001f);
        }

        [Test]
        public void OutOfBounds_Clamps()
        {
            var n = MinimapProjection.WorldToNormalized(new Vector2(99999f, -99999f), WorldMin, WorldMax);
            Assert.AreEqual(1f, n.x, 0.0001f);
            Assert.AreEqual(0f, n.y, 0.0001f);
        }

        [Test]
        public void RectangularWorld_AxesNormalizeIndependently()
        {
            // Halfway across X (22000 wide) but only a quarter across Z (12000 tall)
            var point = new Vector2(0f, -3000f); // 0 -> mid X; -3000 -> 1/4 up from min Z
            var n = MinimapProjection.WorldToNormalized(point, WorldMin, WorldMax);
            Assert.AreEqual(0.5f, n.x, 0.0001f);
            Assert.AreEqual(0.25f, n.y, 0.0001f);
        }
    }
}
