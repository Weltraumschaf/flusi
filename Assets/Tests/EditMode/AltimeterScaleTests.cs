using NUnit.Framework;

namespace Flusi.Tests
{
    public class AltimeterScaleTests
    {
        [Test]
        public void Ground_Level_Points_Both_Needles_Up()
        {
            Assert.AreEqual(0f, AltimeterScale.HundredsAngle(0f), 0.001f);
            Assert.AreEqual(0f, AltimeterScale.ThousandsAngle(0f), 0.001f);
        }

        [Test]
        public void Half_A_Thousand_Puts_Long_Needle_At_Six_OClock()
        {
            Assert.AreEqual(180f, AltimeterScale.HundredsAngle(500f), 0.001f);
            Assert.AreEqual(18f, AltimeterScale.ThousandsAngle(500f), 0.001f);
        }

        [Test]
        public void Long_Needle_Wraps_At_Exactly_One_Thousand()
        {
            Assert.AreEqual(0f, AltimeterScale.HundredsAngle(1000f), 0.001f);
            Assert.AreEqual(36f, AltimeterScale.ThousandsAngle(1000f), 0.001f);
        }

        [Test]
        public void Fifteen_Hundred_Reads_One_And_A_Half()
        {
            Assert.AreEqual(180f, AltimeterScale.HundredsAngle(1500f), 0.001f);
            Assert.AreEqual(54f, AltimeterScale.ThousandsAngle(1500f), 0.001f);
        }

        // The terrain clamp should stop this happening, but the gauge must not
        // produce a negative angle if it ever does.
        [Test]
        public void Negative_Altitude_Wraps_Instead_Of_Going_Negative()
        {
            Assert.AreEqual(324f, AltimeterScale.HundredsAngle(-100f), 0.001f);
            Assert.AreEqual(356.4f, AltimeterScale.ThousandsAngle(-100f), 0.01f);
        }
    }
}
