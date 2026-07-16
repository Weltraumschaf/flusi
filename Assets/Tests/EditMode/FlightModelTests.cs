using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class FlightModelTests
    {
        private static FlightConfig Cfg => FlightConfig.Default;

        private static FlightState Level(float speed) => new FlightState
        {
            Position = Vector3.up * 1000f,
            Heading = 0f, Pitch = 0f, Bank = 0f, Speed = speed
        };

        [Test]
        public void Throttle_Positive_IncreasesSpeed()
        {
            var s = FlightModel.Step(Level(60f), new FlightInput { Throttle = 1f }, 0f, Cfg, 0.1f);
            Assert.Greater(s.Speed, 60f);
        }

        [Test]
        public void Throttle_ClampsAtMaxSpeed()
        {
            var s = Level(Cfg.MaxSpeed);
            for (int i = 0; i < 100; i++)
                s = FlightModel.Step(s, new FlightInput { Throttle = 1f }, 0f, Cfg, 0.1f);
            Assert.LessOrEqual(s.Speed, Cfg.MaxSpeed);
        }

        [Test]
        public void Throttle_ClampsAtMinSpeed()
        {
            var s = Level(Cfg.MinSpeed);
            for (int i = 0; i < 100; i++)
                s = FlightModel.Step(s, new FlightInput { Throttle = -1f }, 0f, Cfg, 0.1f);
            Assert.GreaterOrEqual(s.Speed, Cfg.MinSpeed);
        }

        [Test]
        public void Turn_BanksTowardMaxBank()
        {
            var s = Level(80f);
            s = FlightModel.Step(s, new FlightInput { Turn = 1f }, 0f, Cfg, 0.1f);
            Assert.Greater(s.Bank, 0f);
            Assert.LessOrEqual(s.Bank, Cfg.MaxBankDeg + 0.001f);
        }

        [Test]
        public void Pitch_Input_RaisesPitch_ClampedToMax()
        {
            var s = Level(80f);
            for (int i = 0; i < 100; i++)
                s = FlightModel.Step(s, new FlightInput { Pitch = 1f }, 0f, Cfg, 0.1f);
            Assert.AreEqual(Cfg.MaxPitchDeg, s.Pitch, 0.001f);
        }

        [Test]
        public void AutoLevel_On_NoInput_ConvergesToLevel()
        {
            var s = Level(80f);
            s.Bank = 40f; s.Pitch = 20f;
            for (int i = 0; i < 400; i++)
                s = FlightModel.Step(s, new FlightInput { AutoLevel = true }, 0f, Cfg, 0.1f);
            Assert.AreEqual(0f, s.Bank, 0.5f);
            Assert.AreEqual(0f, s.Pitch, 0.5f);
        }

        [Test]
        public void AutoLevel_Off_HoldsBank()
        {
            var s = Level(80f);
            s.Bank = 40f;
            for (int i = 0; i < 50; i++)
                s = FlightModel.Step(s, new FlightInput { AutoLevel = false }, 0f, Cfg, 0.1f);
            Assert.AreEqual(40f, s.Bank, 0.001f);
        }

        [Test]
        public void AutoLevel_Off_HoldsPitch()
        {
            var s = Level(80f);
            s.Pitch = 20f;
            for (int i = 0; i < 50; i++)
                s = FlightModel.Step(s, new FlightInput { AutoLevel = false }, 0f, Cfg, 0.1f);
            Assert.AreEqual(20f, s.Pitch, 0.001f);
        }

        [Test]
        public void Level_Flight_AdvancesForwardNorth()
        {
            var s = Level(100f); // heading 0 = +Z (north)
            s = FlightModel.Step(s, new FlightInput(), 0f, Cfg, 1f);
            Assert.Greater(s.Position.z, 99f);            // moved ~100 m north
            Assert.AreEqual(0f, s.Position.x, 0.5f);
            Assert.AreEqual(1000f, s.Position.y, 0.5f);   // level: no altitude change
        }

        [Test]
        public void Bank_ProducesHeadingChange()
        {
            var s = Level(100f);
            s.Bank = Cfg.MaxBankDeg;
            float before = s.Heading;
            s = FlightModel.Step(s, new FlightInput { Turn = 1f }, 0f, Cfg, 1f);
            Assert.Greater(Mathf.DeltaAngle(before, s.Heading), 0f);
        }

        [Test]
        public void NoBank_KeepsHeading()
        {
            var s = Level(100f);
            s = FlightModel.Step(s, new FlightInput(), 0f, Cfg, 1f);
            Assert.AreEqual(0f, s.Heading, 0.001f);
        }

        [Test]
        public void BelowGround_ClampsToClearance_AndLevelsDescent()
        {
            var s = Level(100f);
            s.Position = new Vector3(0f, 100f, 0f);
            s.Pitch = -30f; // diving
            float ground = 150f; // ground is above the plane
            s = FlightModel.Step(s, new FlightInput(), ground, Cfg, 0.1f);
            Assert.GreaterOrEqual(s.Position.y, ground + Cfg.GroundClearance - 0.001f);
            Assert.GreaterOrEqual(s.Pitch, 0f); // no longer diving into the ground
        }

        [Test]
        public void AboveGround_NoClamp()
        {
            var s = Level(100f);
            s.Position = new Vector3(0f, 1000f, 0f);
            s = FlightModel.Step(s, new FlightInput(), 0f, Cfg, 0.1f);
            Assert.AreEqual(1000f, s.Position.y, 0.5f);
        }
    }
}
