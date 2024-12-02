using SlugTemplate;
using BepInEx.Logging;

namespace mcevilslug
{
    public class CanPutSlugToBack_Hook
    {

        public delegate bool orig_CanPutSlugToBack(Player self);

        public static bool Evilslug_CanPutSlugToBack_get(orig_CanPutSlugToBack orig, Player self)
        {
            var logger = Logger.CreateLogSource("evil logger");

            logger.LogInfo("entered CanPutSlugToBack_Hook and Evilslug_CanPutSlugToBack_get");

            if (self.slugcatStats.name.value == Plugin.MOD_ID)
            {
                logger.LogInfo("slubase ID check passed, returning false");

                return false;
            }

            logger.LogInfo("slugbase ID check failed, current slugcat ID is " + self.slugcatStats.name);

            return orig(self);
        }
    }
}