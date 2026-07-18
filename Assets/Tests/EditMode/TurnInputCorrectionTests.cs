using NUnit.Framework;
using UnityEngine;

namespace Flusi.Tests
{
    public class TurnInputCorrectionTests
    {
        [Test]
        public void Linux_Player_Inverts_Turn()
            => Assert.AreEqual(-1f, TurnInputCorrection.Apply(1f, RuntimePlatform.LinuxPlayer), 0.001f);

        [Test]
        public void Linux_Editor_Inverts_Turn()
            => Assert.AreEqual(-1f, TurnInputCorrection.Apply(1f, RuntimePlatform.LinuxEditor), 0.001f);

        [Test]
        public void OSX_Player_Leaves_Turn_Unchanged()
            => Assert.AreEqual(1f, TurnInputCorrection.Apply(1f, RuntimePlatform.OSXPlayer), 0.001f);

        [Test]
        public void OSX_Editor_Leaves_Turn_Unchanged()
            => Assert.AreEqual(1f, TurnInputCorrection.Apply(1f, RuntimePlatform.OSXEditor), 0.001f);

        [Test]
        public void Zero_Turn_Stays_Zero_On_Linux()
            => Assert.AreEqual(0f, TurnInputCorrection.Apply(0f, RuntimePlatform.LinuxPlayer), 0.001f);
    }
}
