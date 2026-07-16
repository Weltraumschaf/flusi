using NUnit.Framework;

namespace Flusi.Tests
{
    public class HudFormatTests
    {
        [Test]
        public void Altitude_RoundsToWholeMetres()
            => Assert.AreEqual("1235 m", HudFormat.Altitude(1234.6f));

        [Test]
        public void Speed_ConvertsMpsToKmh()
            => Assert.AreEqual("360 km/h", HudFormat.Speed(100f)); // 100 m/s = 360 km/h
    }
}
