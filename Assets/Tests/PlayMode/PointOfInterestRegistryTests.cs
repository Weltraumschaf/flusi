using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class PointOfInterestRegistryTests
    {
        // The registry is static, so these counts are only meaningful if it starts
        // empty. Clearing after the test is not enough: CockpitPanelSmokeTests
        // loads MainScene and leaves it loaded, and its three points of interest
        // stay registered into whatever runs next.
        [SetUp] public void ClearBefore() => PointOfInterestRegistry.Clear();
        [TearDown] public void ClearAfter() => PointOfInterestRegistry.Clear();

        [Test]
        public void EnabledPoi_RegistersItself()
        {
            var go = new GameObject("A");
            go.AddComponent<Airport>();
            Assert.AreEqual(1, PointOfInterestRegistry.All.Count);
            Assert.AreEqual(PoiKind.Airport, PointOfInterestRegistry.All[0].Kind);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DisabledPoi_Unregisters()
        {
            var go = new GameObject("L");
            go.AddComponent<Landmark>();
            Assert.AreEqual(1, PointOfInterestRegistry.All.Count);
            Object.DestroyImmediate(go);
            Assert.AreEqual(0, PointOfInterestRegistry.All.Count);
        }
    }
}
