using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class PointOfInterestRegistryTests
    {
        [TearDown] public void Clear() => PointOfInterestRegistry.Clear();

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
