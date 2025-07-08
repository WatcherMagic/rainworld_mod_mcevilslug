using BepInEx;
using mcevilslug;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using On.MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "Evil McEvilslug", "0.1.0")]

    class Plugin : BaseUnityPlugin
    {
        public const string MOD_ID = "mcevilslug";

        private const int QUARTER_FOOD_AMOUNT_MUSHROOM = 2;
        private const int FOOD_AMOUNT_KARMAFLOWER = 1;

        private const int MAUL_COUNTER = 20;
        private const int SLUG_TO_BACK_COUNTER = 10;
        private int framesPickupHeld = 0;
        private int framesSlugToBackInput = 0;

        private int minPupsPerCycle = 4; //always 1 lower than the actual minimum due to Unity's Random.Range int overload
        private int maxPupsForceSpawned = 7;

        private const float LEAVE_TRACKS_COUNTER = 30f;
        private float leaveTracksTimer = LEAVE_TRACKS_COUNTER;

        private const int PUP_CHUNK_GRABBED = 0;

        private const float SNIFF_COUNTER = 0f; //20
        private float lastSniff = SNIFF_COUNTER;
        private bool tracksVisible = false;

        private List<AbstractRoom> realizedShelters = new List<AbstractRoom>();

        internal static BindingFlags bfAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public void OnEnable()
        {
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            //register and unregister enums for pup tracks
            On.RainWorld.OnModsEnabled += RainWorld_OnModsEnabled;
            On.RainWorld.OnModsDisabled += RainWorld_OnModsDisabled;

            On.AbstractPhysicalObject.Realize += AbstractPhysicalObject_Realize;

            LoadManualHooks();

            //world hooks
            On.World.SpawnPupNPCs += SpawnPupOnWorldLoad;
            On.AbstractRoom.RealizeRoom += SpawnPupOnShelterRealize;
            //On.World.LoadWorld += ClearList;

            //player hooks
            On.Player.ObjectEaten += AddFood;
            On.Player.GrabUpdate += EvilGrabUpdate;
            On.Player.ThrownSpear += EvilSpearThrow;
            //On.Player.Update += EvilClimb;
            On.Player.Update += EvilPlayerUpdates;
            On.Player.ProcessDebugInputs += EvilDebug;

            //slugnpc hooks
            SlugNPCAI.Update += PupUpdate;

            try
            {
                IL.Player.Update += NoPopcorn;

            }
            catch (Exception e)
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
                UnityEngine.Debug.Log("[evilslug] Realizing track " + self.ID);

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

            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private int SpawnPupOnWorldLoad(On.World.orig_SpawnPupNPCs orig, World self)
        {
            if (ModManager.MSC && self.game.IsStorySession && self.game.StoryCharacter.value == MOD_ID)
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
            if (game.IsStorySession && game.StoryCharacter.value == MOD_ID)
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
            if (ModManager.MSC)
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

                    //SetCritRandDestination(abstractCreature, world);

                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
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
                else if (framesPickupHeld >= MAUL_COUNTER)
                {
                    //UpdatePupAIState(self.grasps[0].grabbed as Player);
                    (self.grasps[0].grabbed as Player).standing = false;
                    self.Grab(self.grasps[0].grabbed, 0, PUP_CHUNK_GRABBED, Creature.Grasp.Shareability.CanNotShare, 0.5f, true, true);
                    self.MaulingUpdate(0);
                }

            }
            if (self.input[1].pckp && !self.input[0].pckp)
            {
                framesPickupHeld = 0;
            }

            orig(self, eu);
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

        // private void EvilClimb(On.Player.orig_Update orig, Player self, bool eu)
        // {
        //     orig(self, eu);

        //     if (self.slugcatStats.name.value == MOD_ID
        //         && self.input[0].AnyDirectionalInput
        //         && self.input[0].jmp
        //         && self.State.alive
        //         && !self.Stunned)
        //     {
        //         TryToClimb(self);
        //     }
        // }

        // private void TryToClimb(Player self)
        // {
        //     //WorldCoordinate currentPos = self.abstractCreature.pos;


        //     //UnityEngine.Debug.Log("trying to climb");
        // }

        private void NoPopcorn(ILContext il)
        {
            try
            {
                var c = new ILCursor(il);

                c.GotoNext(MoveType.After, 
                    x => x.MatchSub(),
                    x => x.MatchStfld(typeof(Player).GetField(nameof(Player.eatExternalFoodSourceCounter))),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld(typeof(Player).GetField(nameof(Player.eatExternalFoodSourceCounter))),
                    x => x.MatchLdcI4(1),
                    x => x.MatchBge(out _));
                ILLabel emitTarget = c.MarkLabel();

                c.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcI4(1),
                    x => x.MatchCallOrCallvirt(typeof(Player).GetMethod(nameof(Player.AddFood))),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcI4(0x2D)); //int32 45
                c.Index += 3;
                ILLabel skipFoodTarget = c.MarkLabel();

                c.GotoLabel(emitTarget);
                c.Emit(OpCodes.Ldarg_0); //load the Player instance onto the stack.
                c.EmitDelegate<Func<Player, bool>>((self) =>
                { // Func will similarly follow the stack, the last type will be your return type
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

            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private void EvilPlayerUpdates(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            LeaveTrack(self);
            Sniff(self);
        }

        private void LeaveTrack(Player self)
        {
            //spawn track at player location
            if (leaveTracksTimer >= LEAVE_TRACKS_COUNTER
            && (self.isNPC || self.isSlugpup))
            {
                //UnityEngine.Debug.Log("[evilslug] attempting to leave track...");

                try
                {
                    AbstractPhysicalObject abstractTrack = new(self.room.world, Register.PupTrack, null,
                    self.room.GetWorldCoordinate(self.bodyChunks[0].pos), self.room.game.GetNewID());
                    Pup_Track track = new Pup_Track(abstractTrack);
                    track.SetPupColor((self.graphicsModule as PlayerGraphics).player.ShortCutColor());
                    track.PlaceInRoom(self.room);
                    //UnityEngine.Debug.Log("[evilslug] placed track! ID: " + track.abstractPhysicalObject.ID);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                }

                leaveTracksTimer = 0;
            }
            leaveTracksTimer += Time.deltaTime;
        }

        private void Sniff(Player self)
        {
            if (self.slugcatStats.name.value == MOD_ID)
            {
                if (tracksVisible == true)
                {
                    if (lastSniff >= Pup_Track._VISIBLE_FOR)
                    {
                        tracksVisible = false;
                    }
                    else
                    {
                        for (int i = 0; i < self.room.physicalObjects.Length; i++)
                        {
                            for (int j = 0; j < self.room.physicalObjects[i].Count; j++)
                            {
                                if (self.room.physicalObjects[i][j] is Pup_Track)
                                {
                                    (self.room.physicalObjects[i][j] as Pup_Track).SetVisibleTrue();
                                    UnityEngine.Debug.Log("[evilslug] tracks are now visible");
                                    Logger.LogInfo("Pup tracks set to visible");
                                }
                            }

                        }
                    }
                }
                else
                {
                    lastSniff += Time.deltaTime;
                    for (int i = 0; i < self.room.physicalObjects.Length; i++)
                    {
                        for (int j = 0; j < self.room.physicalObjects[i].Count; j++)
                        {
                            if (self.room.physicalObjects[i][j] is Pup_Track)
                            {
                                (self.room.physicalObjects[i][j] as Pup_Track).SetVisibleFalse();
                                //UnityEngine.Debug.Log("[evilslug] tracks are no longer visible");
                                Logger.LogInfo("Pup tracks set to invisible");
                            }
                        }

                    }
                }

                if (((ModManager.Watcher && self.input[0].spec)
                || Input.GetKeyDown(KeyCode.T)) && lastSniff >= SNIFF_COUNTER)
                {
                    SniffAnimation(self);
                    lastSniff = 0f;
                    tracksVisible = true;
                }
                else if (ModManager.Watcher && self.input[0].spec || Input.GetKeyDown(KeyCode.T))
                {
                    UnityEngine.Debug.Log("[evilslug] can't sniff yet!");
                    // UnityEngine.Debug.Log("[evilslug] can't sniff! Can sniff in "
                    // + (SNIFF_COUNTER - lastSniff) + " seconds");
                }
            }
        }

        private void SniffAnimation(Player self)
        {
            self.Blink(500);
        }

        // private void UpdatePupAIState(Player npc)
        // {
        //     npc.AI.behaviorType = Register.BeingGrabbed;
        // }

        private void EvilDebug(On.Player.orig_ProcessDebugInputs orig, Player self)
        {
            orig(self);

            if (Input.GetKeyDown("1"))
            {
                AbstractPhysicalObject abstractTrack = new(self.room.world, Register.PupTrack, null,
                    self.room.GetWorldCoordinate(self.bodyChunks[0].pos), self.room.game.GetNewID());
                    Pup_Track debugTrack = new Pup_Track(abstractTrack);
                    debugTrack.SetPupColor(Color.white);
                    debugTrack.PlaceInRoom(self.room);
            }
        }

        private void PupUpdate(SlugNPCAI.orig_Update orig, MoreSlugcats.SlugNPCAI self)
        {
            orig(self);

            if (self.behaviorType == Register.BeingGrabbed)
            {
                UnityEngine.Debug.Log("[evilslug] Pup is being grabbed");
            }
        }
    }
}