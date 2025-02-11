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
        private const float SECONDS_BEFORE_DELETION = 240f;

        private EntityID pupID;

        public Pup_Track(AbstractPhysicalObject abstr/*, EntityID pupSpawnedFrom*/) : base(abstr)
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

            //pupID = pupSpawnedFrom;
            age = 0.0f;
        }

        public override void Update(bool eu)
        {
            base.Update(eu);
            lastRotation = rotation;

            age += Time.deltaTime;
            if (age >= SECONDS_BEFORE_DELETION)
            {
                base.Destroy();
            }
            else if (DetectPlayerCollision(bodyChunks[0], room))
            {
                Burst();
            }
        }

        private bool DetectPlayerCollision(BodyChunk chunk, Room room)
        {
            foreach (Player player in room.PlayersInRoom)
            {
                if ((chunk.pos - player.bodyChunks[0].pos).magnitude < (chunk.rad + player.bodyChunks[0].rad)
                || (chunk.pos - player.bodyChunks[1].pos).magnitude < (chunk.rad + player.bodyChunks[1].rad))
                {
                    //UnityEngine.Debug.Log("[evilslug] Player " + player.SlugCatClass + "collided with puptrack");
                    return true;
                }
                    
            }
            return false;
        }

        private void Burst() 
        {
            if (base.slatedForDeletetion)
            {
                return;
            }
            base.Destroy();
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
            if (base.slatedForDeletetion)
            {
                RemoveSpritesFromContainer(sLeaser);
            }

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

                //sprite._color = Custom.hexToColor(pupID.ToString());
                //sprite._alpha = age * SECONDS_BEFORE_DELETION / 100;
            }

            // float decay = age * SECONDS_BEFORE_DELETION / 100;
            // if (decay > 90.0f) {  }
            // if (decay < 90.0f) {  }
            // if (decay < 80.0f) {  }
            // if (decay < 70.0f) {  }
            // if (decay < 60.0f) {  }
            // if (decay < 50.0f) {  }
            // if (decay < 40.0f) {  }
            // if (decay < 30.0f) {  }
            // if (decay < 20.0f) {  }
            // if (decay < 10.0f) {  }
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

        private void RemoveSpritesFromContainer(RoomCamera.SpriteLeaser sLeaser)
        {
            foreach (FSprite sprite in sLeaser.sprites)
            {
                sprite.RemoveFromContainer();
            }
        }
    }
}
