using System.Reflection;
using MonoMod.RuntimeDetour;
using SlugTemplate;

namespace mcevilslug
{
    public class CanPutSlugToBack_Hook
    {
        public delegate bool orig_CanPutSlugToBack(Player self);

        public static bool Evilslug_CanPutSlugToBack_get(orig_CanPutSlugToBack orig, Player self)
        {
            if (self.slugcatStats.name.value == Plugin.MOD_ID)
            {
                return false;
            }

            return orig(self);
        }
    }
}