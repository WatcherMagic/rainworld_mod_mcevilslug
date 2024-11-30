using BepInEx;
using IL.MoreSlugcats;
using MoreSlugcats;
using On.MoreSlugcats;
using RWCustom;
using UnityEngine;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "Evil McEvilslug", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        private const string MOD_ID = "mcevilslug";

        private const int QUARTER_FOOD_AMOUNT_MUSHROOM = 2;
        private const int FOOD_AMOUNT_KARMAFLOWER = 1;

        // Add hooks & register enums
        public void OnEnable()
        {
            Logger.LogInfo("McEvilslug Enabled");

            On.AbstractRoom.RealizeRoom += evilSpawnPup;
            On.Player.ObjectEaten += addFood;
            On.Player.GrabUpdate += killPup;
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
                            StaticWorld.GetCreatureTemplate(MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.SlugNPC),
                            null,
                            new WorldCoordinate(self.index, -1, -1, 0),
                            game.GetNewID());
                        self.AddEntity(abstractCreature);
                        (abstractCreature.state as MoreSlugcats.PlayerNPCState).foodInStomach = 1;

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
                    for (int i = QUARTER_FOOD_AMOUNT_MUSHROOM; i > 0; i--)
                    {
                        self.AddQuarterFood();
                    }
                }
            }

            orig(self, edible);
        }

        private void killPup(On.Player.orig_GrabUpdate orig, Player self, bool eu)
        {
            if (self.grasps[0] != null && self.slugcatStats.name.value == MOD_ID 
                && self.grasps[0].grabbed is Creature)
            {
                if (self.input[0].pckp 
                    && (self.grasps[0].grabbed as Creature).GetType() == typeof(Player))
                {
                    Logger.LogInfo("Pressed pickup while holding creature "
                        + (self.grasps[0].grabbed as Creature).GetType().Name);
                }
            }

            orig(self, eu);
        }
    }
}