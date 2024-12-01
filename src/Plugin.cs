using BepInEx;
using mcevilslug;
using MonoMod.RuntimeDetour;
using System.Reflection;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "Evil McEvilslug", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        public const string MOD_ID = "mcevilslug";

        private const int QUARTER_FOOD_AMOUNT_MUSHROOM = 2;
        private const int FOOD_AMOUNT_KARMAFLOWER = 1;
        private const int PICKUP_COUNTER = 30;
        private int framesPickupHeld = 0;

        BindingFlags propFlags = BindingFlags.Instance | BindingFlags.Public;
        BindingFlags myMethodFlags = BindingFlags.Static | BindingFlags.Public;

        // Add hooks & register enums
        public void OnEnable()
        {
            GenerateManualHooks();

            On.AbstractRoom.RealizeRoom += evilSpawnPup;
            On.Player.ObjectEaten += addFood;
            On.Player.GrabUpdate += killPup;
            //On.Player.CanMaulCreature += evilCanMaulSlug;
        }

        private void GenerateManualHooks()
        {
            Hook canPutSlugToBackHook = new Hook(
                typeof(Player).GetProperty("CanPutSlugToBack", propFlags).GetGetMethod(),
                typeof(CanPutSlugToBack_Hook).GetMethod("Evilslug_CanPutSlugToBack_get", myMethodFlags));
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
                && self.grasps[0].grabbed is Creature && !(self.grasps[0].grabbed as Creature).dead)
            {
                if (self.input[0].pckp 
                    && (self.grasps[0].grabbed as Creature).GetType() == typeof(Player))
                {
                    framesPickupHeld += 1;
                    if (framesPickupHeld >= PICKUP_COUNTER)
                    {
                        Creature creature = self.grasps[0].grabbed as Creature;                        
                        self.Grab(creature, 0, 1, Creature.Grasp.Shareability.CanOnlyShareWithNonExclusive, 0.5f, true, false);
                        self.MaulingUpdate(0);
                        framesPickupHeld = 0;
                    }
                }
            }

            orig(self, eu);
        }

        /*private bool evilCanMaulSlug(On.Player.orig_CanMaulCreature orig, Player self, Creature crit)
        {
            //still doesn't fix spup not taking damage

            if (!crit.dead && self.slugcatStats.name.value == MOD_ID && (crit is Player))
            {
                return true;
            }

            return orig(self, crit);
        }*/
    }
}