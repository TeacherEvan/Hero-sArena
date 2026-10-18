using Godot;

namespace HeroArena
{
    /// <summary>
    /// Environmental destruction drama: destroyed cover bursts into spinning
    /// chunk sprites that arc, bounce once and fade; a smoke puff and an
    /// additive spark burst sell the blast; and an expanding ground ring
    /// flash fakes light propagation across the floor. Every texture is
    /// generated in code at startup (soft squares, radial dots, a ring band)
    /// — zero new art assets.
    ///
    /// Shard spread is deterministic per world position (the same crate
    /// always shatters the same way) via DebrisBurstPlan.SeedFromPosition,
    /// and all planning/ballistics math lives in the Godot-free
    /// DebrisBurstPlan + DebrisShardState (covered by DebrisBurstPlanTests).
    /// This node only maps state onto pooled Sprite2D/CPUParticles2D nodes.
    /// </summary>
    public partial class DestructionDrama : Node2D
    {
        private const int ShardPoolSize = 64;
        private const int RingPoolSize = 4;
        private const float ShardGravity = 920f;
        private const float ShardDrag = 0.6f;
        private const float ShardBaseScale = 0.35f;

        private struct RingFx
        {
            public bool Active;
            public float Age;
            public float Life;
            public float MaxScale;
        }

        private readonly DebrisShardState[] _shardStates = new DebrisShardState[ShardPoolSize];
        private readonly Color[] _shardTints = new Color[ShardPoolSize];
        private readonly Sprite2D[] _shardSprites = new Sprite2D[ShardPoolSize];
        private readonly DebrisBurstPlan.Shard[] _plan = new DebrisBurstPlan.Shard[DebrisBurstPlan.MaxShards];

        private readonly RingFx[] _rings = new RingFx[RingPoolSize];
        private readonly Sprite2D[] _ringSprites = new Sprite2D[RingPoolSize];

        // Scratch structs reused each frame to avoid per-active-shard allocations.
        private Vector2 _shardPos;
        private Color _shardModulate;
        private Vector2 _ringScale;
        private Color _ringModulate;
        private readonly Color _ringBaseColor = new Color(1f, 0.85f, 0.6f);

        private CpuParticles2D[] _smoke = null!;
        private CpuParticles2D[] _sparks = null!;
        private int _nextSmoke;
        private int _nextSpark;
        private int _overflowCursor;

        public override void _Ready()
        {
            // Code-generated gradients upscale far past their source size;
            // the project-wide Nearest filter would band them. Children
            // (sprites + particle emitters) inherit this filter.
            TextureFilter = CanvasItem.TextureFilterEnum.Linear;

            var shardTexture = ShardTexture();
            for (int i = 0; i < ShardPoolSize; i++)
            {
                var sprite = new Sprite2D
                {
                    Texture = shardTexture,
                    Visible = false,
                };
                AddChild(sprite);
                _shardSprites[i] = sprite;
                _shardTints[i] = Colors.White;
            }

            var ringTexture = RingTexture();
            for (int i = 0; i < RingPoolSize; i++)
            {
                var sprite = new Sprite2D
                {
                    Texture = ringTexture,
                    Visible = false,
                };
                AddChild(sprite);
                _ringSprites[i] = sprite;
            }

            var smokeTexture = SoftDotTexture(64);
            _smoke = new[]
            {
                MakeEmitter(smokeTexture, 14, 1.1f, new Vector2(0f, -22f), 90f, 40f, 95f, false,
                    new Color(0.52f, 0.54f, 0.62f, 0.55f)),
                MakeEmitter(smokeTexture, 14, 1.1f, new Vector2(0f, -22f), 90f, 40f, 95f, false,
                    new Color(0.52f, 0.54f, 0.62f, 0.55f)),
            };

            var sparkTexture = SoftDotTexture(16);
            _sparks = new[]
            {
                MakeEmitter(sparkTexture, 10, 0.45f, new Vector2(0f, 600f), 40f, 140f, 320f, true,
                    new Color(1f, 0.8f, 0.35f, 1f)),
                MakeEmitter(sparkTexture, 10, 0.45f, new Vector2(0f, 600f), 40f, 140f, 320f, true,
                    new Color(1f, 0.8f, 0.35f, 1f)),
            };

            if (EventBus.Instance != null)
                EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
            SetProcess(false);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
            base._ExitTree();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            bool any = false;

            for (int i = 0; i < ShardPoolSize; i++)
            {
                ref var s = ref _shardStates[i];
                if (s.Life <= 0f) continue;

                s.Step(dt, ShardGravity, ShardDrag);
                var sprite = _shardSprites[i];
                if (s.Finished)
                {
                    s.Life = 0f;
                    sprite.Visible = false;
                    continue;
                }

                any = true;
                _shardPos.X = s.X;
                _shardPos.Y = s.Y;
                sprite.GlobalPosition = _shardPos;
                sprite.Rotation = s.Rotation;
                var tint = _shardTints[i];
                _shardModulate = tint;
                _shardModulate.A = tint.A * s.Alpha;
                sprite.Modulate = _shardModulate;
            }

            for (int i = 0; i < RingPoolSize; i++)
            {
                ref var r = ref _rings[i];
                if (!r.Active) continue;

                r.Age += dt;
                float t = r.Age / r.Life;
                if (t >= 1f)
                {
                    r.Active = false;
                    _ringSprites[i].Visible = false;
                    continue;
                }

                any = true;
                float ease = 1f - (1f - t) * (1f - t);
                _ringScale.X = (0.25f + ease) * r.MaxScale;
                _ringScale.Y = _ringScale.X;
                _ringSprites[i].Scale = _ringScale;
                _ringModulate = _ringBaseColor;
                _ringModulate.A = (1f - t) * 0.75f;
                _ringSprites[i].Modulate = _ringModulate;
            }

            if (!any) SetProcess(false);
        }

        private void OnEnvironmentDestroyed(Vector2 pos, float radius)
        {
            float power = 1f + radius / 128f;
            int count = DebrisBurstPlan.Generate(
                DebrisBurstPlan.SeedFromPosition(pos.X, pos.Y),
                DebrisBurstPlan.CountForRadius(radius),
                power,
                _plan);

            for (int i = 0; i < count; i++)
            {
                int idx = FindFreeShard();
                if (idx < 0) continue; // pool full; drop this shard instead of corrupting an active one
                ref var s = ref _shardStates[idx];
                s = DebrisShardState.FromPlan(_plan[i], pos.X, pos.Y);
                _shardTints[idx] = ShardTint(i);

                var sprite = _shardSprites[idx];
                sprite.Visible = true;
                sprite.GlobalPosition = pos;
                sprite.Rotation = s.Rotation;
                sprite.Scale = Vector2.One * (ShardBaseScale * s.Scale);
                sprite.Modulate = _shardTints[idx];
            }

            SpawnRing(pos, radius);
            BurstParticles(pos, radius);
            SetProcess(true);
        }

        private int FindFreeShard()
        {
            for (int i = 0; i < ShardPoolSize; i++)
            {
                if (_shardStates[i].Life <= 0f) return i;
            }
            // Pool exhausted: return -1 and let the caller skip spawning rather
            // than silently overwriting an active shard (audit P2-1 / R3).
            return -1;
        }

        private void SpawnRing(Vector2 pos, float radius)
        {
            int idx = -1;
            for (int i = 0; i < RingPoolSize; i++)
            {
                if (!_rings[i].Active)
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) return;

            _rings[idx] = new RingFx
            {
                Active = true,
                Age = 0f,
                Life = 0.45f,
                MaxScale = Mathf.Clamp(radius / 26f, 1.2f, 6f),
            };
            var sprite = _ringSprites[idx];
            sprite.Visible = true;
            sprite.GlobalPosition = pos;
            sprite.Scale = Vector2.One * (0.25f * _rings[idx].MaxScale);
            sprite.Modulate = new Color(1f, 0.85f, 0.6f, 0.75f);
        }

        private void BurstParticles(Vector2 pos, float radius)
        {
            float scale = Mathf.Clamp(radius / 64f, 0.6f, 2.2f);

            var smoke = _smoke[_nextSmoke++ & 1];
            smoke.Scale = Vector2.One * scale;
            smoke.GlobalPosition = pos;
            smoke.Restart();

            var spark = _sparks[_nextSpark++ & 1];
            spark.Scale = Vector2.One * scale;
            spark.GlobalPosition = pos;
            spark.Restart();
        }

        private CpuParticles2D MakeEmitter(
            Texture2D texture, int amount, float lifetime, Vector2 gravity, float damping,
            float velocityMin, float velocityMax, bool additive, Color color)
        {
            var emitter = new CpuParticles2D
            {
                Texture = texture,
                Emitting = false,
                OneShot = true,
                Explosiveness = 1f,
                Amount = amount,
                Lifetime = lifetime,
                LifetimeRandomness = 0.5f,
                Direction = new Vector2(0f, -1f),
                Spread = 180f,
                InitialVelocityMin = velocityMin,
                InitialVelocityMax = velocityMax,
                Gravity = gravity,
                DampingMin = damping,
                DampingMax = damping,
                ScaleAmountMin = 0.5f,
                ScaleAmountMax = 1.8f,
                ColorRamp = FadeRamp(),
                Color = color,
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                EmissionRectExtents = new Vector2(6f, 6f),
            };
            if (additive)
                emitter.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            AddChild(emitter);
            return emitter;
        }

        /// <summary>Alpha fade used by both emitters (full → gone over lifetime).</summary>
        private static Gradient FadeRamp()
        {
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 1f));
            g.SetOffset(0, 0f);
            g.SetColor(1, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(1, 1f);
            return g;
        }

        /// <summary>Soft-edged square chunk (rotating it reads as tumbling debris).</summary>
        private static GradientTexture2D ShardTexture()
        {
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 1f));
            g.SetOffset(0, 0f);
            g.SetColor(1, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(1, 1f);
            g.AddPoint(0.72f, new Color(1f, 1f, 1f, 1f));
            return MakeTexture(g, 32, GradientTexture2D.FillEnum.Square);
        }

        /// <summary>Ring band for the floor flash (transparent center and edge).</summary>
        private static GradientTexture2D RingTexture()
        {
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(0, 0f);
            g.SetColor(1, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(1, 1f);
            g.AddPoint(0.78f, new Color(1f, 1f, 1f, 1f));
            return MakeTexture(g, 64, GradientTexture2D.FillEnum.Radial);
        }

        /// <summary>Radial soft dot for smoke puffs and sparks.</summary>
        private static GradientTexture2D SoftDotTexture(int size)
        {
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 1f));
            g.SetOffset(0, 0f);
            g.SetColor(1, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(1, 1f);
            return MakeTexture(g, size, GradientTexture2D.FillEnum.Radial);
        }

        private static GradientTexture2D MakeTexture(Gradient gradient, int size, GradientTexture2D.FillEnum fill)
            => new()
            {
                Gradient = gradient,
                Width = size,
                Height = size,
                Fill = fill,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
            };

        private static Color ShardTint(int index)
        {
            // Deterministic per-index shade; every 4th shard glows like an ember.
            bool ember = (index & 3) == 0;
            float shade = 0.82f + ((index * 37) % 13) * 0.02f;
            Color baseColor = ember ? new Color(1f, 0.48f, 0.18f) : new Color(0.58f, 0.55f, 0.5f);
            return new Color(baseColor.R * shade, baseColor.G * shade, baseColor.B * shade, 1f);
        }
    }
}
