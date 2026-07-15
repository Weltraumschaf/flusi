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
    }
}
