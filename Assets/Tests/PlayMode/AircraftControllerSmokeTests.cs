using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Flusi.Tests
{
    public class AircraftControllerSmokeTests
    {
        [UnityTest]
        public IEnumerator Aircraft_MovesForward_OverTime()
        {
            var go = new GameObject("Aircraft");
            go.transform.position = new Vector3(0f, 1000f, 0f);
            go.AddComponent<AircraftController>();

            Vector3 start = go.transform.position;
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate();

            Assert.Greater((go.transform.position - start).magnitude, 1f,
                "Aircraft should have moved under its own speed.");
            Object.Destroy(go);
        }
    }
}
