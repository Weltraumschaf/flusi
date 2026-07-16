using NUnit.Framework;

namespace Flusi.Tests
{
    public class FlightDerivationsTests
    {
        [Test]
        public void Level_Flight_Has_Zero_Vertical_Speed()
            => Assert.AreEqual(0f, FlightDerivations.VerticalSpeed(100f, 0f), 0.001f);

        // sin(30) == 0.5, so half the airspeed becomes climb rate.
        [Test]
        public void Nose_Up_Thirty_Degrees_Climbs_At_Half_Airspeed()
            => Assert.AreEqual(50f, FlightDerivations.VerticalSpeed(100f, 30f), 0.001f);

        [Test]
        public void Nose_Down_Thirty_Degrees_Descends_At_Half_Airspeed()
            => Assert.AreEqual(-50f, FlightDerivations.VerticalSpeed(100f, -30f), 0.001f);

        [Test]
        public void Straight_Up_Climbs_At_Full_Airspeed()
            => Assert.AreEqual(100f, FlightDerivations.VerticalSpeed(100f, 90f), 0.001f);

        [Test]
        public void SpeedKmh_Converts_Metres_Per_Second()
            => Assert.AreEqual(360f, FlightDerivations.SpeedKmh(100f), 0.001f);
    }
}
