using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class AircraftStateRefTests
    {
        /// Minimal Unity-object source. Deliberately not AircraftController:
        /// this test must not depend on that component's Awake/OnDestroy
        /// lifecycle, only on the destroyed-object semantics.
        private class StubAircraft : MonoBehaviour, IAircraftState
        {
            public float AltitudeMeters => 0f;
            public float SpeedMetersPerSecond => 0f;
            public float HeadingDegrees => 0f;
            public float PitchDegrees => 0f;
            public float BankDegrees => 0f;
            public Vector3 WorldPosition => Vector3.zero;
            public bool AutoLevelOn => true;
            public bool GearDown => true;
        }

        /// A plain C# source, as a test double would be. It is not a
        /// UnityEngine.Object and must not be mistaken for a destroyed one.
        private class FakeAircraft : IAircraftState
        {
            public float AltitudeMeters => 0f;
            public float SpeedMetersPerSecond => 0f;
            public float HeadingDegrees => 0f;
            public float PitchDegrees => 0f;
            public float BankDegrees => 0f;
            public Vector3 WorldPosition => Vector3.zero;
            public bool AutoLevelOn => true;
            public bool GearDown => true;
        }

        [Test]
        public void Null_Is_Not_Alive()
            => Assert.IsFalse(AircraftStateRef.IsAlive(null));

        [Test]
        public void Live_Component_Is_Alive()
        {
            var go = new GameObject("stub");
            IAircraftState state = go.AddComponent<StubAircraft>();
            Assert.IsTrue(AircraftStateRef.IsAlive(state));
            Object.DestroyImmediate(go);
        }

        // The test that matters: this is what a naive `state != null` fails.
        [Test]
        public void Destroyed_Component_Is_Not_Alive()
        {
            var go = new GameObject("stub");
            IAircraftState state = go.AddComponent<StubAircraft>();
            Object.DestroyImmediate(go);

            Assert.IsFalse(AircraftStateRef.IsAlive(state),
                "a destroyed source must not read as alive through an interface field");
        }

        [Test]
        public void Plain_CSharp_Source_Is_Alive()
            => Assert.IsTrue(AircraftStateRef.IsAlive(new FakeAircraft()),
                "a non-Unity implementation has no destroyed state and must stay usable");
    }
}
