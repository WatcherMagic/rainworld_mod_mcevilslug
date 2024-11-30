using System;
using BepInEx;
using UnityEngine;
using SlugBase.Features;
using static SlugBase.Features.FeatureTypes;
using IL;
using On;
using MonoMod.Cil;
using System.Reflection;
using MonoMod.RuntimeDetour;
using MoreSlugcats;

namespace mcevilslug
{
    public class EvilSlugEnums
    {
        public static SlugcatStats.Name McEvil;

        public static void RegisterValues()
        {
            McEvil = new SlugcatStats.Name("McEvil", true);
        }

        public static void UnregisterValues()
        {
            if (McEvil != null)
            {
                McEvil.Unregister();
                McEvil = null;
            }
        }
    }
}