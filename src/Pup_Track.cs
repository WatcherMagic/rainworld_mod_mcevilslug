using RWCustom;
using UnityEngine;

namespace mcevilslug
{
    public class Pup_Track : PhysicalObject, IDrawable
    {
        public Vector2 rotation;
        public Vector2 lastRotation;
        public float darkness;
        public float lastDarkness;

        private float age;
        private const float SECONDS_BEFORE_DELETION = 240.0f;

        public Pup_Track(AbstractPhysicalObject abstr) : base(abstr)
        {
            bodyChunks = new BodyChunk[] { new BodyChunk(this, 0, Vector2.zero, 4, 0.05f) };
            bodyChunkConnections = new BodyChunkConnection[0]; //empty array for 1 chunk
            bodyChunks[0].collideWithObjects = false;
            gravity = 0f;
            airFriction = 0f;
            waterFriction = 0f;
            surfaceFriction = 0f;
            collisionLayer = 1;
            bounce = 0f;
            buoyancy = 0f;

            age = 0.0f;
        }

        public override void Update(bool eu)
        {
            base.Update(eu);
            lastRotation = rotation;

            age += Time.deltaTime;
            if (age >= SECONDS_BEFORE_DELETION
            || detectPlayerCollision(bodyChunks[0], room))
            {
                base.Destroy();
                UnityEngine.Debug.Log("[evilslug] Puptrack destroyed");
            }
        }

        private bool detectPlayerCollision(BodyChunk chunk, Room room)
        {
            foreach (Player player in room.PlayersInRoom)
            {
                if ((chunk.pos - player.bodyChunks[0].pos).magnitude < (chunk.rad + player.bodyChunks[0].rad))
                {
                    UnityEngine.Debug.Log("[evilslug] Player " + player.SlugCatClass + "collided with puptrack");
                    return true;
                }
            }
            return false;
        }

        public override void PlaceInRoom(Room placeRoom)
        {
            base.PlaceInRoom(placeRoom);
            
            firstChunk.HardSetPosition(placeRoom.MiddleOfTile(abstractPhysicalObject.pos.Tile));
            
            rotation = Custom.RNV(); //Custom.RNV() sets random direction
            lastRotation = rotation;
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[] { new FSprite("atlases/puptracks/pup_track") { scale = 1f } };
            AddToContainer(sLeaser, rCam, null);
        }

        public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            Vector2 pos = Vector2.Lerp(firstChunk.lastPos, firstChunk.pos, timeStacker);
            Vector2 rt = Vector3.Slerp(lastRotation, rotation, timeStacker);
            lastDarkness = darkness;
            //The formula for determining darkness is a template
            darkness = rCam.room.Darkness(pos) * (1f - rCam.room.LightSourceExposure(pos));
            if (darkness != lastDarkness)
                ApplyPalette(sLeaser, rCam, rCam.currentPalette);
            foreach (FSprite sprite in sLeaser.sprites)
            {
                sprite.x = pos.x - camPos.x;
                sprite.y = pos.y - camPos.y;
                sprite.rotation = Custom.VecToDeg(rt);
            }
        }

        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            //item is unaffected by room pallet
        }

        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            //If newContainer is null, then it is assigned an Items container
            newContatiner = newContatiner ?? rCam.ReturnFContainer("Foreground");
            foreach (FSprite sprite in sLeaser.sprites)
            {
                sprite.RemoveFromContainer();
                newContatiner.AddChild(sprite);
            }
        }
    }
}
