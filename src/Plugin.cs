using BepInEx;
using IL.RWCustom;
using mcevilslug;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
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

        private int minPupsPerCycle = 4; //always 1 lower than the actual minimum due to Unity's Random.Range int overload
        private int maxPupsForceSpawned = 7;

        private const float LEAVE_TRACKS_COUNTER = 10f;
        private float leaveTracksTimer = 0.0f;

        private List<AbstractRoom> realizedShelters = new List<AbstractRoom>();

        internal static BindingFlags bfAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;


        public void OnEnable()
        {
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            On.RainWorld.OnModsEnabled += RainWorld_OnModsEnabled;
            On.RainWorld.OnModsDisabled += RainWorld_OnModsDisabled;

            On.AbstractPhysicalObject.Realize += AbstractPhysicalObject_Realize;

            LoadManualHooks();

            On.World.SpawnPupNPCs += SpawnPupOnWorldLoad;
            On.AbstractRoom.RealizeRoom += SpawnPupOnShelterRealize;
            //On.World.LoadWorld += ClearList;
            On.Player.ObjectEaten += AddFood;
            On.Player.GrabUpdate += EvilGrabUpdate;
            On.Player.ThrownSpear += EvilSpearThrow;
            On.Player.Update += EvilClimb;
            On.Player.Update += LeaveTrack;

            try
            {
                IL.Player.Update += NoPopcorn;

            } catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private void LoadResources(RainWorld rainWorld)
        {
            Futile.atlasManager.LoadImage("atlases/puptracks/pup_track");
        }

        private void RainWorld_OnModsEnabled(On.RainWorld.orig_OnModsEnabled orig,
            RainWorld self, ModManager.Mod[] newlyEnabledMods)
        {
            orig(self, newlyEnabledMods);
            Register.RegisterValues();
        }

        private void RainWorld_OnModsDisabled(On.RainWorld.orig_OnModsDisabled orig, 
            RainWorld self, ModManager.Mod[] newlyDisabledMods)
        {
            orig(self, newlyDisabledMods);
            foreach (ModManager.Mod mod in newlyDisabledMods)
                if (mod.id == MOD_ID)
                    Register.UnregisterValues();
        }

        private void AbstractPhysicalObject_Realize(On.AbstractPhysicalObject.orig_Realize orig, AbstractPhysicalObject self)
        {
            orig(self);
            if (self.type == Register.PupTrack)
            {
                self.realizedObject = new Pup_Track(self);
            }
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
                //UnityEngine.Debug.Log("[evilslug] Region pup chance is " + self.region.regionParams.slugPupSpawnChance);
                //UnityEngine.Debug.Log("Evilslug: slugPupMaxCount is " + self.game.GetStorySession.slugPupMaxCount);

                Logger.LogInfo("SpawnPupOnWorldLoad() initiated");
                //Logger.LogInfo("Pup spawn chance is " + self.region.regionParams.slugPupSpawnChance);
                //Logger.LogInfo("slugPupMaxCount is " + self.game.GetStorySession.slugPupMaxCount);

                AbstractRoom spawnRoom = null;
                int pupsThisCycle = UnityEngine.Random.Range(minPupsPerCycle, maxPupsForceSpawned);
                UnityEngine.Debug.Log("Evilslug: Force spawning " + pupsThisCycle + " pups this cycle");
                Logger.LogInfo("Force spawning " + pupsThisCycle + " pups this cycle");

                for (int p = 0; p < pupsThisCycle; p++)
                {
                    int randRoomIndex = UnityEngine.Random.Range(0, self.abstractRooms.Length + 1);
                    spawnRoom = self.abstractRooms[randRoomIndex];

                    if (spawnRoom.offScreenDen)
                    {
                        Logger.LogInfo("Attempted pup spawn in offscreen den failed. Reattempting in new room...");
                        pupsThisCycle++;
                    }
                    else
                    {
                        SpawnPup(self.game, self, spawnRoom);
                    }
                }
            }

            return orig(self);
        }

        private void SpawnPupOnShelterRealize(On.AbstractRoom.orig_RealizeRoom orig, AbstractRoom self, World world, RainWorldGame game)
        {
            if (game.StoryCharacter.value == MOD_ID)
            {
                if (ModManager.MSC 
                    && self.realizedRoom == null
                    && !self.offScreenDen
                    && self.shelter
                    && !world.singleRoomWorld
                    && self.name != game.GetStorySession.saveState.denPosition)
                {
                    Logger.LogInfo("Attempting to spawn SlugNPC in realized shelter...");
                    // bool hasBeenRealized = false;

                    // if (realizedShelters.Any())
                    // {
                    //     foreach (AbstractRoom shelter in realizedShelters)
                    //     {
                    //         if (shelter.name == self.name)
                    //         {
                    //             hasBeenRealized = true;
                    //             Logger.LogInfo("Failed! This shelter has already been realized once");
                    //         }
                    //     }
                    // }

                    // if (!hasBeenRealized)
                    // {
                        float spawn = UnityEngine.Random.Range(0f, 10f);
                        if (spawn <= 1.7f)
                        {
                            SpawnPup(game, world, self);
                        }
                        else
                        {
                            Logger.LogInfo("Spawn failed due to random chance");
                        }
                        realizedShelters.Add(self);
                    //}
                }
            }
            
            orig(self, world, game);
        }

        // private void ClearList(On.World.orig_LoadWorld orig, World self, SlugcatStats.Name slugcatNumber, List<AbstractRoom> abstractRoomsList, int[] swarmRooms, int[] shelters, int[] gates)
        // {
        //     Logger.LogInfo("Clearing realized shelter list on world load");
        //     if (realizedShelters.Any())
        //     {
        //         Logger.LogInfo("Shelters realized previously:\n" + String.Join(",\n", realizedShelters));
        //         realizedShelters.Clear();
        //     }
        //     else
        //     {
        //         Logger.LogInfo("realizedShelters list was empty");
        //     }
        // }

        private void SpawnPup(RainWorldGame game, World world, AbstractRoom room)
        {
            //copied from AbstractRoom.RealizeRoom()
            AbstractCreature abstractCreature = new AbstractCreature(world,
                StaticWorld.GetCreatureTemplate(MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.SlugNPC),
                null,
                new WorldCoordinate(room.index, -1, -1, 0),
                game.GetNewID());

            try
            {
                room.AddEntity(abstractCreature);
                (abstractCreature.state as MoreSlugcats.PlayerNPCState).foodInStomach = 1;

                Logger.LogInfo(abstractCreature.GetType().ToString() + " " + abstractCreature.ID + " spawned in " + abstractCreature.Room.name);
                UnityEngine.Debug.Log("[evilslug] " + abstractCreature.GetType().ToString() + " " + abstractCreature.ID + " spawned in " + abstractCreature.Room.name);
            
                SetCritRandDestination(abstractCreature, world);

            } catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private void SetCritRandDestination(AbstractCreature crit, World world)
        {
            int goalIndex = UnityEngine.Random.Range(0, world.abstractRooms.Length - 1);
            AbstractRoom goal = world.abstractRooms[goalIndex];
            if (goal.offScreenDen)
            {
                SetCritRandDestination(crit, world);
            }
            else
            {
                WorldCoordinate pos = goal.RandomNodeInRoom();
                crit.abstractAI.SetDestination(pos);
                UnityEngine.Debug.Log("[evilslug] Set pup " + crit.ID + "'s destination to " + goal.name);
                Logger.LogInfo("Set pup " + crit.ID + "'s destination to " + goal.name);
            }
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
                        Logger.LogInfo("Placing pup on back");
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
                
                firstChunk.vel.y *= UnityEngine.Random.Range(0f, 1f);
                firstChunk.vel.x *= UnityEngine.Random.Range(-1f, 1f);
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
                    x => x.MatchLdcI4(0x2D)); //int32 45

                c.Index += 3;
                ILLabel skipFoodTarget = c.MarkLabel();
                c.Index -= 3;

                c.Emit(OpCodes.Ldarg_0); //load the Player instance onto the stack.
                c.EmitDelegate<Func<Player, bool>>((self) => { // Func will similarly follow the stack, the last type will be your return type
                    if (self.slugcatStats.name.value == MOD_ID) // Change this to whatever your code is
                    {
                        return true;
                    }
                    return false;
                });
                // Since the return type is on the stack we can use it here
                c.Emit(OpCodes.Brtrue, skipFoodTarget);


                //Logger.LogInfo(il.ToString());
                //UnityEngine.Debug.Log(il.ToString());

            } catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private void LeaveTrack(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            //spawn track at player location
            if (leaveTracksTimer >= LEAVE_TRACKS_COUNTER 
            && (self.isNPC || self.isSlugpup))
            {
                try
                {
                    AbstractPhysicalObject ab = new(self.room.world, Register.PupTrack, null,
                    self.room.GetWorldCoordinate(self.bodyChunks[0].pos), self.room.game.GetNewID());
                    // causes NullReferenceException if pup track is placed on collision layers 0 and 2
                    //EntityID pupID = self.abstractCreature.ID;

                    Pup_Track track = new Pup_Track(ab/*, pupID*/);

                    track.PlaceInRoom(self.room);
                    
                } catch (Exception ex)
                {
                    Logger.LogError(ex);
                }

                leaveTracksTimer = 0;
            }
            leaveTracksTimer += UnityEngine.Time.deltaTime;
        }
    }
}