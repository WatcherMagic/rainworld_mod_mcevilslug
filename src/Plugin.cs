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
using BepInEx.Logging;
using mcevilslug;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "Evil McEvilslug", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        private const string MOD_ID = "mcevilslug";

        private const int FOOD_AMOUNT_MUSHROOM = 1;
        private const int FOOD_AMOUNT_KARMAFLOWER = 2;

        // Add hooks & register enums
        public void OnEnable()
        {
            Logger.LogInfo("McEvilslug Enabled");

            EvilSlugEnums.RegisterValues();

            On.AbstractRoom.RealizeRoom += evilSpawnPup;
            On.Player.ObjectEaten += addFood;
            On.RainWorld.OnModsDisabled += UnregisterEvilEnums;
        }

        private void evilSpawnPup(On.AbstractRoom.orig_RealizeRoom orig, AbstractRoom self, World world, RainWorldGame game)
        {
            if (game.StoryCharacter.value == MOD_ID)
            {
                Logger.LogInfo("evilSpawnPup() slugbase ID check passed");

                if (self.realizedRoom == null && !self.offScreenDen)
                {
                    int maxCycleWithoutPup = 2;
                    if (ModManager.MSC
                        && self.shelter
                        && !world.singleRoomWorld
                        && game.GetStorySession.saveState.miscWorldSaveData.cyclesSinceLastSlugpup >= maxCycleWithoutPup
                        && self.name != game.GetStorySession.saveState.denPosition)
                    {
                        Logger.LogInfo("Attempting to spawn SlugNPC...");

                        //copied from AbstractRoom.RealizeRoom()
                        AbstractCreature abstractCreature = new AbstractCreature(world,
                            StaticWorld.GetCreatureTemplate(MoreSlugcatsEnums.CreatureTemplateType.SlugNPC),
                            null,
                            new WorldCoordinate(self.index, -1, -1, 0),
                            game.GetNewID());
                        self.AddEntity(abstractCreature);
                        (abstractCreature.state as PlayerNPCState).foodInStomach = 1;

                        Logger.LogInfo("Spawn successful!");
                        Logger.LogInfo(abstractCreature.GetType().ToString() + " " + abstractCreature.ID + " spawned in " + abstractCreature.Room.name);
                        
                        //removed below to increase chances of pups spawning & of multiple pups spawning
                        //is run in orig() anyway
                        //game.GetStorySession.saveState.miscWorldSaveData.cyclesSinceLastSlugpup = -game.GetStorySession.saveState.miscWorldSaveData.cyclesSinceLastSlugpup;
                    }
                }
            }
            
            orig(self, world, game);
        }

        private void addFood(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            if (self.slugcatStats.name.value == MOD_ID)
            {
                if (edible.GetType().Name == nameof(KarmaFlower))
                {
                    self.AddFood(FOOD_AMOUNT_KARMAFLOWER);
                }
                if (edible.GetType().Name == nameof(Mushroom))
                {
                    self.AddFood(FOOD_AMOUNT_MUSHROOM);
                }
            }

            orig(self, edible);
        }

        private void UnregisterEvilEnums(On.RainWorld.orig_OnModsDisabled orig, RainWorld self, ModManager.Mod[] newlyDisabledMods)
        {
            orig(self, newlyDisabledMods);
            foreach (ModManager.Mod mod in newlyDisabledMods)
            {
                if (mod.id == MOD_ID)
                {
                    EvilSlugEnums.UnregisterValues();
                }
            }
        }
    }
}