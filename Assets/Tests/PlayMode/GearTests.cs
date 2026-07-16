using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Flusi.Tests
{
    public class GearTests
    {
        [UnityTest]
        public IEnumerator Gear_Starts_Down_And_Toggles()
        {
            var go = new GameObject("Aircraft");
            var controller = go.AddComponent<AircraftController>();
            yield return null;

            var state = (IAircraftState)controller;
            Assert.IsTrue(state.GearDown, "gear should start down");

            controller.ToggleGear();
            Assert.IsFalse(state.GearDown, "gear should retract on toggle");

            controller.ToggleGear();
            Assert.IsTrue(state.GearDown, "gear should extend again on second toggle");

            Object.Destroy(go);
        }
    }
}
