namespace Flusi
{
    /// Guards for holding an IAircraftState across the aircraft's destruction.
    ///
    /// Lives beside IAircraftState rather than in Cockpit/, because it is a
    /// property of the seam itself — World/Minimap.cs needs it too.
    public static class AircraftStateRef
    {
        /// True when `state` is present and safe to read this frame.
        ///
        /// A plain `state != null` is NOT enough. Unity overloads == on
        /// UnityEngine.Object so a destroyed object compares equal to null, but
        /// that overload is chosen by the reference's STATIC type. Through an
        /// IAircraftState-typed field the static type is a plain C# interface,
        /// so the overload never applies: a destroyed MonoBehaviour compares as
        /// non-null and the next property read throws MissingReferenceException
        /// — once per frame, forever.
        ///
        /// The pattern match below recovers the UnityEngine.Object static type,
        /// which brings the overload back. A non-Unity implementation (a test
        /// double) is not a UnityEngine.Object and has no destroyed state, so it
        /// stays alive while non-null.
        public static bool IsAlive(IAircraftState state)
            => state != null && !(state is UnityEngine.Object o && o == null);
    }
}
