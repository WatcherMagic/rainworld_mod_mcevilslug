using IL.MoreSlugcats;

namespace mcevilslug
{
    public static class Register
    {
        public static AbstractPhysicalObject.AbstractObjectType PupTrack;
        public static MoreSlugcats.SlugNPCAI.BehaviorType BeingGrabbed;

        public static void RegisterValues()
        {
            PupTrack = new AbstractPhysicalObject.AbstractObjectType("PupTrack", true);
            BeingGrabbed = new MoreSlugcats.SlugNPCAI.BehaviorType("BeingGrabbed", true);
        }

        public static void UnregisterValues()
        {
            AbstractPhysicalObject.AbstractObjectType pupTrack = PupTrack;
            pupTrack?.Unregister();
            PupTrack = null;

            MoreSlugcats.SlugNPCAI.BehaviorType beingGrabbed = BeingGrabbed;
            beingGrabbed?.Unregister();
            BeingGrabbed = null;
        }
    }
}
