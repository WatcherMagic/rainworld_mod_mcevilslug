using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "Evil McEvilslug", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        public const string MOD_ID = "mcevilslug";

        private const int QUARTER_FOOD_AMOUNT_MUSHROOM = 2;
        private const int FOOD_AMOUNT_KARMAFLOWER = 1;

        private const int PICKUP_COUNTER = 20;
        private const int SLUG_TO_BACK_COUNTER = 10;
        private int framesPickupHeld = 0;
        private int framesSlugToBackInput = 0;

        // Add hooks & register enums
        public void OnEnable()
        {
            On.AbstractRoom.RealizeRoom += evilSpawnPup;
            On.Player.ObjectEaten += addFood;
            On.Player.GrabUpdate += evilGrabUpdate;
            //On.Player.ThrowObject += tossSpear;

            try
            {
                IL.Player.Update += noPopcorn;

            } catch (Exception e)
            {
                Logger.LogError(e);
            }
            
            //On.Player.CanMaulCreature += evilCanMaulSlug;
        }

        private void evilSpawnPup(On.AbstractRoom.orig_RealizeRoom orig, AbstractRoom self, World world, RainWorldGame game)
        {
            if (game.StoryCharacter.value == MOD_ID)
            {
                Logger.LogInfo("evilSpawnPup() slugbase ID check passed");

                if (self.realizedRoom == null && !self.offScreenDen)
                {
                    int maxCycleWithoutPup = 3;
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
                        game.GetStorySession.saveState.miscWorldSaveData.cyclesSinceLastSlugpup = -game.GetStorySession.saveState.miscWorldSaveData.cyclesSinceLastSlugpup;

                        Logger.LogInfo("Spawn successful!");
                        Logger.LogInfo(abstractCreature.GetType().ToString() + " " + abstractCreature.ID + " spawned in " + abstractCreature.Room.name);

                        UnityEngine.Debug.Log("Evilslug: " + abstractCreature.GetType().ToString()
                            + " " + abstractCreature.ID + " spawned in " + abstractCreature.Room.name);

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

        private void evilGrabUpdate(On.Player.orig_GrabUpdate orig, Player self, bool eu)
        {

            if (self.input[0].pckp && self.grasps[0] != null && self.slugcatStats.name.value == MOD_ID
                && self.grasps[0].grabbed is Creature
                && !(self.grasps[0].grabbed as Creature).dead
                && (self.grasps[0].grabbed as Creature).GetType() == typeof(Player))
            {

                framesPickupHeld += 1;
                if (self.input[1].AnyDirectionalInput && !self.input[0].AnyDirectionalInput)
                {
                    framesPickupHeld = 0;
                    framesSlugToBackInput = 0;
                }
                if (self.input[0].AnyDirectionalInput)
                {
                    framesSlugToBackInput += 1;
                    if (framesSlugToBackInput >= SLUG_TO_BACK_COUNTER)
                    {
                        self.slugOnBack.SlugToBack(self.grasps[0].grabbed as Player);
                        framesSlugToBackInput = 0;
                    }
                }
                else if (framesPickupHeld >= PICKUP_COUNTER)
                {
                    killPup(self);
                }
                
            }
            if (self.input[1].pckp && !self.input[0].pckp)
            {
                framesPickupHeld = 0;
            }

            orig(self, eu);
        }

        private void killPup(Player self)
        {
            Creature creature = self.grasps[0].grabbed as Creature;
            self.Grab(creature, 0, 1, Creature.Grasp.Shareability.CanOnlyShareWithNonExclusive, 0.5f, true, false);
            self.MaulingUpdate(0);
        }

        //private void tossSpear(On.Player.orig_ThrowObject orig, Player self, int grasp, bool eu)
        //{
        //    if (self.slugcatStats.name.value == MOD_ID 
        //        && (!ModManager.Expedition 
        //            || (ModManager.Expedition && !self.room.game.rainWorld.ExpeditionMode))) {

        //        //
        //    }

        //    orig(self, grasp, eu);
        //}

        private void noPopcorn(ILContext il)
        {
            try
            {
                var c = new ILCursor(il);

                c.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcI4(1),
                    x => x.MatchCallOrCallvirt(typeof(Player).GetMethod(nameof(Player.AddFood))),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcI4(45), //0x2D
                    x => x.MatchStfld(typeof(Player).GetField(nameof(Player.dontEatExternalFoodSourceCounter))));

                c.Index += 3;
                ILLabel skipFoodTarget = c.MarkLabel();
                c.Index -= 1;
                ILLabel addFoodTarget = c.MarkLabel();
                c.Index -= 2;
                //
                c.Emit(OpCodes.Br, skipFoodTarget);
                //c.Emit(OpCodes.Brfalse, addFoodTarget);

                UnityEngine.Debug.Log(il.ToString());

            } catch (Exception e)
            {
                Logger.LogError(e);
            }
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