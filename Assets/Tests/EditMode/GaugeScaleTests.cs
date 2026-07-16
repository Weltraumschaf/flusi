using NUnit.Framework;

namespace Flusi.Tests
{
    public class GaugeScaleTests
    {
        // Airspeed calibration: 0..500 km/h, 0 deg at 12 o'clock, +320 clockwise.
        [Test]
        public void Value_At_Min_Gives_StartAngle()
            => Assert.AreEqual(0f, GaugeScale.ValueToAngle(0f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Value_At_Max_Gives_StartPlusSweep()
            => Assert.AreEqual(320f, GaugeScale.ValueToAngle(500f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Value_At_Midpoint_Gives_HalfSweep()
            => Assert.AreEqual(160f, GaugeScale.ValueToAngle(250f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Value_Below_Min_Clamps_To_StartAngle()
            => Assert.AreEqual(0f, GaugeScale.ValueToAngle(-100f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Value_Above_Max_Clamps_To_StartPlusSweep()
            => Assert.AreEqual(320f, GaugeScale.ValueToAngle(9999f, 0f, 500f, 0f, 320f), 0.001f);

        [Test]
        public void Negative_Sweep_Runs_CounterClockwise()
            => Assert.AreEqual(-30f, GaugeScale.ValueToAngle(55f, -55f, 55f, 30f, -60f), 0.001f);

        // VSI calibration (spec 3.2): -100..+100 m/s, start 185, sweep 170.
        // Zero must land on 9 o'clock, i.e. 270 degrees.
        [Test]
        public void VerticalSpeed_Zero_Points_At_Nine_OClock()
            => Assert.AreEqual(270f, GaugeScale.ValueToAngle(0f, -100f, 100f, 185f, 170f), 0.001f);

        // Turn coordinator calibration (spec 3.2): -55..+55 bank, start -30, sweep 60.
        [Test]
        public void TurnCoordinator_Level_Bank_Gives_Level_Symbol()
            => Assert.AreEqual(0f, GaugeScale.ValueToAngle(0f, -55f, 55f, -30f, 60f), 0.001f);
    }
}
