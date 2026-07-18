using UnityEngine;

namespace Flusi
{
    /// Unity's Input System native keyboard backend swaps the left/right arrow
    /// scan codes on Linux (pitch/up-down is unaffected); this corrects for it
    /// at the platform level since the binding asset itself is correct.
    public static class TurnInputCorrection
    {
        public static float Apply(float rawTurn, RuntimePlatform platform)
        {
            bool isLinux = platform == RuntimePlatform.LinuxPlayer
                           || platform == RuntimePlatform.LinuxEditor;
            return isLinux ? -rawTurn : rawTurn;
        }
    }
}
