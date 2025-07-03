using IL.RWCustom;
using Mono.CompilerServices.SymbolWriter;
using UnityEngine;

namespace mcevilslug
{
    public class Pup_Track : PhysicalObject, IDrawable
    {
        private Vector2 _spawnPos;
        private const float _BOB_RADIUS = 500f;
        private bool _bobUp = true;
        private bool _visible = false;
        private LightSource _light = null;
        private const float _LIGHT_KILLED_FADE_IN_SECONDS = 0.5f;
        private float _lightKilledFade = 0f;

        private float _age = 0f;
        private float _secondsVisible = 0f;
        private const float _SECONDS_BEFORE_DELETION = 5f; //240
        public const float _VISIBLE_FOR = 30f;
        private Color _pupColor;
        private Color _lightColor;
        private readonly Color _invisibleColor = new Color(0f, 0f, 0f);
        private const float _LIGHT_RAD = 40f;
        private bool _beingKilled = false;
        private const float _MIN_BRIGHTNESS = 0.3f;

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

            _age = 0.0f;

            _spawnPos = firstChunk.pos;
        }

        public override void Update(bool eu)
        {
            base.Update(eu);

            Bob();

            if (_light != null)
            {
                Debug.Log("[evilslug] " + base.abstractPhysicalObject.ID + "alpha: " + _light.Alpha);

                //light.setAlpha = 1f; //100f / age * SECONDS_BEFORE_DELETION;
                if (!_visible)
                {
                    room.RemoveObject(_light);
                    _light = null;
                }
                else
                {
                    Flicker();
                    _light.setPos = firstChunk.pos;
                    _light.setAlpha = 1f - (_MIN_BRIGHTNESS + (_age / _SECONDS_BEFORE_DELETION * _MIN_BRIGHTNESS));
                }
            }
            else
            {
                if (_pupColor.g < 0.2f && _pupColor.b < 0.2f && _pupColor.r < 0.2f)
                {
                    _lightColor = _pupColor + new Color(0.3f - _pupColor.g, 0.3f - _pupColor.b, 0.3f - _pupColor.r);
                }
                else
                {
                    _lightColor = _pupColor;
                }

                _light = new LightSource(firstChunk.pos, false, _lightColor, this)
                {
                    setPos = firstChunk.pos,
                    setAlpha = new float?(1f),
                    setRad = _LIGHT_RAD
                    //affectedByPaletteDarkness = 0.5f
                };
                room.AddObject(_light);
            }

            if (_visible)
            {
                _secondsVisible += Time.deltaTime;
                if (_secondsVisible >= _VISIBLE_FOR)
                {
                    SetVisibleFalse();
                }
            }

            _age += Time.deltaTime;
            if (_age >= _SECONDS_BEFORE_DELETION && !_beingKilled)
            {
                Kill();
            }
            else if (DetectPlayerCollision(bodyChunks[0], room))
            {
                Burst();
            }
        }

        private void Bob()
        {
            if (_bobUp)
            {
                //firstChunk.vel.y += 1.2f;
            }
            else
            {
                //firstChunk.vel.y -= 1.2f;
            }

            // if (Vector2.Distance(spawnPos, firstChunk.pos) >= BOB_RADIUS)
            // {
            //     bobUp = !bobUp;
            // }
        }

        private void Flicker()
        {
            //newRad = LIGHT_RAD * Random.Range(0.8f, 1.2f);
        }

        public void SetPupColor(Color c)
        {
            _pupColor = c;
        }

        public void SetVisibleFalse()
        {
            _visible = false;
        }

        public void SetVisibleTrue()
        {
            _visible = true;
        }

        public bool Visibility()
        {
            return _visible;
        }

        private void Kill()
        {
            _beingKilled = true;
            if (slatedForDeletetion)
            {
                _light = null;
                return;
            }
            // if (_lightKilledFade < _LIGHT_KILLED_FADE_IN_SECONDS)
            // {
            //     _lightKilledFade += Time.deltaTime;
            //     _light.setAlpha = _MIN_BRIGHTNESS - (_lightKilledFade / _LIGHT_KILLED_FADE_IN_SECONDS * 0.3f);
            // }
            // else
            // {
                base.Destroy();
            //}
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
            Kill();
        }

        public override void PlaceInRoom(Room placeRoom)
        {
            firstChunk.HardSetPosition(placeRoom.MiddleOfTile(abstractPhysicalObject.pos.Tile));

            base.PlaceInRoom(placeRoom);
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[] { new FSprite("atlases/puptracks/pup_track") { scale = 1f } };
            AddToContainer(sLeaser, rCam, null);
        }

        public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            if (slatedForDeletetion)
            {
                RemoveSpritesFromContainer(sLeaser);
            }

            Vector2 pos = Vector2.Lerp(firstChunk.lastPos, firstChunk.pos, timeStacker);

            foreach (FSprite sprite in sLeaser.sprites)
            {
                sprite.x = pos.x - camPos.x;
                sprite.y = pos.y - camPos.y;

                if (!_visible && sprite._color != _invisibleColor)
                {
                    sprite._color = _invisibleColor;
                }
                else if (sprite._color == _invisibleColor)
                {
                    sprite._color = _pupColor;
                }
            }

            ApplyPalette(sLeaser, rCam, rCam.currentPalette);
        }

        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            sLeaser.sprites[0].color = _pupColor;
            //sLeaser.sprites[0]._alpha = 100f / age * SECONDS_BEFORE_DELETION;
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
