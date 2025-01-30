using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mcevilslug
{
    public static class Register
    {
        public static AbstractPhysicalObject.AbstractObjectType PupTrack;

        public static void RegisterValues()
        {
            PupTrack = new AbstractPhysicalObject.AbstractObjectType("PupTrack", true);
        }

        public static void UnregisterValues()
        {
            AbstractPhysicalObject.AbstractObjectType pupTrack = PupTrack;
            pupTrack?.Unregister();
            PupTrack = null;
        }
    }
}
