using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class MinimapProjectionTests
    {
        [Test]
        public void Origin_MapsToCentre()
        {
            var n = MinimapProjection.WorldToNormalized(Vector2.zero, 10000f);
            Assert.AreEqual(0.5f, n.x, 0.0001f);
            Assert.AreEqual(0.5f, n.y, 0.0001f);
        }

        [Test]
        public void PositiveCorner_MapsNearOne()
        {
            var n = MinimapProjection.WorldToNormalized(new Vector2(5000f, 5000f), 10000f);
            Assert.AreEqual(1f, n.x, 0.0001f);
            Assert.AreEqual(1f, n.y, 0.0001f);
        }

        [Test]
        public void OutOfBounds_Clamps()
        {
            var n = MinimapProjection.WorldToNormalized(new Vector2(99999f, -99999f), 10000f);
            Assert.AreEqual(1f, n.x, 0.0001f);
            Assert.AreEqual(0f, n.y, 0.0001f);
        }
    }
}
