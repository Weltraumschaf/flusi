using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Flusi.Tests
{
    public class CockpitPanelSmokeTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null; // let Awake/OnEnable settle
        }

        [UnityTest]
        public IEnumerator Panel_Builds_With_All_Six_Gauges()
        {
            yield return null;

            var panel = Object.FindFirstObjectByType<CockpitPanel>();
            Assert.IsNotNull(panel, "CockpitPanel should be in the scene");

            Assert.AreEqual(3, Object.FindObjectsByType<NeedleGauge>(
                FindObjectsSortMode.None).Length,
                "airspeed, turn coordinator and vertical speed");
            Assert.IsNotNull(Object.FindFirstObjectByType<Altimeter>());
            Assert.IsNotNull(Object.FindFirstObjectByType<AttitudeIndicator>());
            Assert.IsNotNull(Object.FindFirstObjectByType<HeadingIndicator>());
        }

        [UnityTest]
        public IEnumerator Panel_Hides_In_Orbit_View_And_Returns()
        {
            yield return null;

            var rig = Object.FindFirstObjectByType<CameraRig>();
            Assert.IsNotNull(rig, "CameraRig should be in the scene");

            var root = GameObject.Find("PanelRoot");
            Assert.IsNotNull(root, "PanelRoot should be in the scene");
            Assert.IsTrue(root.activeSelf, "panel visible in cockpit view");
            Assert.AreEqual(ViewMode.Cockpit, rig.Current);

            rig.ToggleView();
            yield return null;
            Assert.AreEqual(ViewMode.Orbit, rig.Current);
            Assert.IsFalse(root.activeSelf, "panel hidden in orbit view");

            rig.ToggleView();
            yield return null;
            Assert.AreEqual(ViewMode.Cockpit, rig.Current);
            Assert.IsTrue(root.activeSelf, "panel returns in cockpit view");
        }
    }
}
