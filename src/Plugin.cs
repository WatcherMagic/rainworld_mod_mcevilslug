using BepInEx;
using mcevilslug;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

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

        private int minPupsPerCycle = 2; //will always be 1 lower than the actual minimum due to Unity's Random.Range int overload
        private int maxPupsForceSpawned = 5;

        internal static BindingFlags bfAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;


        public void OnEnable()
        {
            LoadManualHooks();

            On.World.SpawnPupNPCs += SpawnPupOnWorldLoad;
            On.AbstractRoom.RealizeRoom += SpawnPupOnShelterRealize;
            On.Player.ObjectEaten += AddFood;
            On.Player.GrabUpdate += EvilGrabUpdate;
            On.Player.ThrownSpear += EvilSpearThrow;
            On.Player.Update += EvilClimb;

            try
            {
                IL.Player.Update += NoPopcorn;

            } catch (Exception e)
            {
                Logger.LogError(e);
            }
            
            //On.Player.CanMaulCreature += evilCanMaulSlug;
        }

        private void LoadManualHooks()
        {
            try
            {
                _ = new Hook(
                    typeof(StoryGameSession).GetProperty(nameof(StoryGameSession.slugPupMaxCount), bfAll).GetGetMethod(),
                    typeof(SlugPupMaxCount_Hook).GetMethod(nameof(SlugPupMaxCount_Hook.MaxPups_Hook), bfAll));

            } catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private int SpawnPupOnWorldLoad(On.World.orig_SpawnPupNPCs orig, World self)
        {
            if (self.game.StoryCharacter.value == MOD_ID)
            {
                UnityEngine.Debug.Log("Evilslug: Region pup chance is " + self.region.regionParams.slugPupSpawnChance);
                UnityEngine.Debug.Log("Evilslug: slugPupMaxCount is " + self.game.GetStorySession.slugPupMaxCount);

                Logger.LogInfo("SpawnPupOnWorldLoad() initiated");
                Logger.LogInfo("Pup spawn chance is " + self.region.regionParams.slugPupSpawnChance);
                Logger.LogInfo("slugPupMaxCount is " + self.game.GetStorySession.slugPupMaxCount);

                AbstractRoom spawnRoom = null;
                int pupsThisCycle = UnityEngine.Random.Range(minPupsPerCycle, maxPupsForceSpawned);
                UnityEngine.Debug.Log("Evilslug: Force spawning " + pupsThisCycle + " pups this cycle");
                Logger.LogInfo("Force spawning " + pupsThisCycle + " pups this cycle");

                for (int p = 0; p < pupsThisCycle; p++)
                {
                    int randRoomIndex = UnityEngine.Random.Range(0, self.abstractRooms.Length + 1);
                    spawnRoom = self.abstractRooms[randRoomIndex];

                    SpawnPup(self.game, self, spawnRoom);
                }
            }

            return orig(self);
        }

        private void SpawnPupOnShelterRealize(On.AbstractRoom.orig_RealizeRoom orig, AbstractRoom self, World world, RainWorldGame game)
        {
            if (game.StoryCharacter.value == MOD_ID)
            {
                Logger.LogInfo("SpawnPupOnShelterRealize() slugbase ID check passed");

                if (ModManager.MSC 
                    && self.realizedRoom == null
                    && !self.offScreenDen
                    && self.shelter
                    && !world.singleRoomWorld
                    && self.name != game.GetStorySession.saveState.denPosition)
                {
                    Logger.LogInfo("Attempting to spawn SlugNPC...");

                    float spawn = UnityEngine.Random.Range(0f, 10f);
                    if (spawn <= 1.7f)
                    {
                        SpawnPup(game, world, self);
                    }
                    else
                    {
                        Logger.LogInfo("Spawn failed due to random chance");
                    }
                    
                }
            }
            
            orig(self, world, game);
        }

        private void SpawnPup(RainWorldGame game, World world, AbstractRoom room)
        {
            //copied from AbstractRoom.RealizeRoom()
            AbstractCreature abstractCreature = new AbstractCreature(world,
                StaticWorld.GetCreatureTemplate(MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.SlugNPC),
                null,
                new WorldCoordinate(room.index, -1, -1, 0),
                game.GetNewID());
            room.AddEntity(abstractCreature);
            (abstractCreature.state as MoreSlugcats.PlayerNPCState).foodInStomach = 1;

            Logger.LogInfo(abstractCreature.GetType().ToString() + " " + abstractCreature.ID + " spawned in " + abstractCreature.Room.name);

            UnityEngine.Debug.Log("Evilslug: " + "Slugpup " + abstractCreature.ID + " spawned in " + abstractCreature.Room.name);
        }

        private void AddFood(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
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

        private void EvilGrabUpdate(On.Player.orig_GrabUpdate orig, Player self, bool eu)
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
                        UnityEngine.Debug.Log("Evilslug: putting pup to back");
                        self.slugOnBack.SlugToBack(self.grasps[0].grabbed as Player);
                        framesSlugToBackInput = 0;
                    }
                }
                else if (framesPickupHeld >= PICKUP_COUNTER)
                {
                    KillPup(self);
                }
                
            }
            if (self.input[1].pckp && !self.input[0].pckp)
            {
                framesPickupHeld = 0;
            }

            orig(self, eu);
        }

        private void KillPup(Player self)
        {
            Creature creature = self.grasps[0].grabbed as Creature;
            self.Grab(creature, 0, 1, Creature.Grasp.Shareability.CanOnlyShareWithNonExclusive, 0.5f, true, false);
            self.MaulingUpdate(0);
        }

        private void EvilSpearThrow(On.Player.orig_ThrownSpear orig, Player self, Spear spear)
        {
            orig(self, spear);

            if (self.slugcatStats.name.value == MOD_ID)
            {
                BodyChunk firstChunk = spear.firstChunk;
                firstChunk.vel.y = firstChunk.vel.y * 0.33f;
                spear.SetRandomSpin();
            }
        }

        private void EvilClimb(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            if (self.slugcatStats.name.value == MOD_ID
                && self.input[0].AnyDirectionalInput
                && self.input[0].jmp
                && self.State.alive
                && !self.Stunned)
            {
                TryToClimb(self);
            }
        }

        private void TryToClimb(Player self)
        {
            //WorldCoordinate currentPos = self.abstractCreature.pos;


            //UnityEngine.Debug.Log("trying to climb");
        }

        private void NoPopcorn(ILContext il)
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