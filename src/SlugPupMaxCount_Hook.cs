using BepInEx.Logging;
using SlugTemplate;
using System;

namespace mcevilslug
{
    public class SlugPupMaxCount_Hook
    {
        public delegate int orig_SlugPupMaxCount(StoryGameSession sesh);

        public static int MaxPups_Hook(orig_SlugPupMaxCount orig, StoryGameSession session)
        {
            try
            {
                if (session.game.StoryCharacter.value == Plugin.MOD_ID)
                {
                    return 100;
                }

            } catch (Exception e)
            {
                ManualLogSource log = Logger.CreateLogSource("EvilSlug SlugPupMaxCount_Hook");
                log.LogError(e);
            }

            return orig(session);
        }
    }
}
