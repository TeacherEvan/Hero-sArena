using System;

namespace HeroArena
{
    /// <summary>
    /// Godot-free simulation of the arena's ambient wildlife (the illusion
    /// layer behind <c>scripts/vfx/AmbientWildlife.cs</c>): a fixed pool of
    /// critters that wander around home points, drift with the wind, flee as a
    /// group when combat erupts (<see cref="Panic"/>), hide entirely while a
    /// boss wave has the world holding its breath (<see cref="HideAll"/>), and
    /// — when the arena is calm — let the bolder species drift curiously
    /// toward the idle hero (<see cref="SetCuriosity"/>).
    ///
    /// Population is scheduled (critters fade in one at a time, fade out and
    /// deactivate when the target shrinks) so the arena never visibly pops.
    /// All motion math lives here; the node only maps <see cref="GetCritter"/>
    /// state onto pooled sprites. Extracted per the AGENTS.md pattern; covered
    /// by WildlifeDirectorStateTests.
    /// </summary>
    public sealed class WildlifeDirectorState
    {
        /// <summary>Fixed critter pool size. The arena is a stage, not a zoo.</summary>
        public const int MaxCritters = 64;

        /// <summary>Seconds between spawn fades while a deficit exists.</summary>
        public const float SpawnInterval = 0.35f;

        /// <summary>Fade durations for spawning and despawning critters.</summary>
        public const float FadeInSeconds = 1.6f;
        public const float FadeOutSeconds = 1.2f;

        /// <summary>Margin from the arena edges that spawns and motion respect.</summary>
        public const float EdgeMargin = 120f;

        /// <summary>Critters are not spawned within this radius of the hero.</summary>
        public const float HeroSpawnClearance = 220f;

        /// <summary>Distance the curious keep from the hero (stand-off, then back off).</summary>
        public const float CuriosityStandoff = 140f;

        /// <summary>Species with flee sensitivity at/below this approach the idle hero.</summary>
        public const float CuriosityBoldnessThreshold = 0.5f;

        /// <summary>Radius around home within which wander targets are picked.</summary>
        public const float WanderRadius = 180f;

        /// <summary>Critters push apart when closer than this (soft separation, px).</summary>
        public const float SeparationRadius = 26f;

        /// <summary>How firmly separation steers a critter (px/s at contact).</summary>
        public const float SeparationWeight = 26f;

        /// <summary>Firefly flash duty cycle: fraction of the period spent flashing.</summary>
        public const float FlashDuty = 0.06f;

        /// <summary>Firefly flash period in seconds (research: real fireflies flash every ~2–7 s).</summary>
        public const float FlashPeriod = 4.4f;

        /// <summary>Dim baseline so unlit fireflies are still faintly trackable.</summary>
        public const float FlashDimBase = 0.1f;

        /// <summary>
        /// Per-species parameters. Biomes pick 1–2 of these (see
        /// <see cref="BiomeAtmosphereState"/>); the distinction is purely data.
        /// </summary>
        public struct SpeciesProfile
        {
            public string Name;
            public float Speed;             // wander cruise speed, px/s
            public float PanicSpeed;        // flee speed, px/s
            public float Size;              // draw scale factor for the node
            public float Glow01;            // additive glow strength for the node
            public float Height01;          // 0 = ground skitter, 1 = air drift
            public float FleeSensitivity01; // scales the panic radius they notice
            public float AccentR;           // accent/glow colour (0..1) for the node
            public float AccentG;
            public float AccentB;
        }

        /// <summary>One pooled critter. Positions are world-space floats.</summary>
        public struct Critter
        {
            public bool Active;
            public bool Despawning;
            public float X;
            public float Y;
            public float VX;
            public float VY;
            public float TargetX;
            public float TargetY;
            public float WanderTimer;
            public float PanicTime;
            public float PanicX;
            public float PanicY;
            public float HideTime;
            public float Fade;     // 0..1 alpha the node applies
            public float Phase;    // stable per-critter animation phase
            public int Species;    // index into the configured profiles
            public float HomeX;
            public float HomeY;
        }

        private readonly Critter[] _critters = new Critter[MaxCritters];
        private SpeciesProfile[] _profiles = Array.Empty<SpeciesProfile>();
        private Random _rng = new Random(1);
        private int _targetPopulation;
        private float _spawnCooldown;
        private float _arenaCx;
        private float _arenaCy;
        private float _arenaHalfW = 1024f;
        private float _arenaHalfH = 1024f;
        private float _heroX;
        private float _heroY;
        private bool _heroSet;
        private float _curiosity;

        /// <summary>Slots with <see cref="Critter.Active"/> set (fading in, visible, or fading out).</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Target population this director schedules toward.</summary>
        public int TargetPopulation => _targetPopulation;

        /// <summary>Sets the species roster and desired population; resets the pool.</summary>
        public void Configure(SpeciesProfile[] profiles, int targetPopulation, int seed)
        {
            _profiles = profiles ?? Array.Empty<SpeciesProfile>();
            _targetPopulation = Math.Clamp(targetPopulation, 0, MaxCritters);
            _rng = new Random(seed);
            Array.Clear(_critters);
            _spawnCooldown = 0f;
            ActiveCount = 0;
        }

        /// <summary>
        /// Raises or lowers the desired population without resetting the pool.
        /// Lowering fades the farthest-from-hero critters out one by one;
        /// raising schedules spawns at <see cref="SpawnInterval"/>. The node
        /// uses this to thin wildlife as waves escalate.
        /// </summary>
        public void SetTargetPopulation(int value)
            => _targetPopulation = Math.Clamp(value, 0, MaxCritters);

        /// <summary>Sets the rectangle critters live in (world-space centre + half extents).</summary>
        public void SetArena(float centerX, float centerY, float halfWidth, float halfHeight)
        {
            _arenaCx = centerX;
            _arenaCy = centerY;
            _arenaHalfW = MathF.Max(64f, halfWidth);
            _arenaHalfH = MathF.Max(64f, halfHeight);
        }

        /// <summary>The hero's position: spawn clearance, curiosity target, despawn priority.</summary>
        public void SetHero(float x, float y)
        {
            _heroX = x;
            _heroY = y;
            _heroSet = true;
        }

        /// <summary>0 = absorbed in combat (nobody approaches), 1 = deep calm (bold critters investigate).</summary>
        public void SetCuriosity(float factor01) => _curiosity = Math.Clamp(factor01, 0f, 1f);

        /// <summary>
        /// Optional scar sampler (0 = clean .. 1 = scarred). Wander targets
        /// falling in scarred ground are re-rolled up to
        /// <see cref="ScarAttempts"/> times; a critter never strands because
        /// of it — the last candidate wins.
        /// </summary>
        public Func<float, float, float>? ScarSampler { get; set; }

        /// <summary>Wander targets at or above this scar strength are re-rolled.</summary>
        public const float ScarAvoidThreshold = 0.35f;

        /// <summary>Max wander-target re-rolls under scar pressure.</summary>
        public const int ScarAttempts = 4;

        /// <summary>
        /// Advance the simulation one tick. <paramref name="windX"/>/<paramref name="windY"/>
        /// come from <see cref="AmbientWindState"/> so creatures drift with the same
        /// wind that moves the ground and particles.
        /// </summary>
        public void Update(float dt, float windX, float windY)
        {
            if (dt <= 0f) return;
            UpdateSpawning(dt);

            for (int i = 0; i < MaxCritters; i++)
            {
                ref var c = ref _critters[i];
                if (!c.Active) continue;

                UpdateFade(ref c, dt);
                if (!c.Active) continue; // faded out this tick

                if (c.HideTime > 0f)
                {
                    c.HideTime -= dt;
                    if (c.HideTime > 0f) continue; // frozen + faded while hiding
                    PickWanderTarget(ref c);
                }

                if (c.PanicTime > 0f)
                    c.PanicTime -= dt;

                Steer(i, ref c, dt, windX, windY);
                Integrate(ref c, dt);
                c.Phase += dt;
            }
        }

        /// <summary>
        /// Startles every visible critter within a scaled radius of the
        /// epicentre and throws it away from it. <paramref name="strength01"/>
        /// scales how long the panic lasts.
        /// </summary>
        public void Panic(float x, float y, float radius, float strength01)
        {
            float strength = Math.Clamp(strength01, 0f, 1f);
            for (int i = 0; i < MaxCritters; i++)
            {
                ref var c = ref _critters[i];
                if (!c.Active || c.Despawning || c.HideTime > 0f) continue;

                var spec = SpeciesAt(c.Species);
                float scaled = radius * (0.7f + 0.6f * spec.FleeSensitivity01);
                float dx = c.X - x;
                float dy = c.Y - y;
                if (dx * dx + dy * dy > scaled * scaled) continue;

                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float panicSeconds = 1.2f + 3.3f * strength;
                c.PanicTime = MathF.Max(c.PanicTime, panicSeconds);
                c.PanicX = x;
                c.PanicY = y;
                c.WanderTimer = 0f;

                if (dist > 0.001f)
                {
                    float kick = spec.PanicSpeed * 0.7f * (0.5f + 0.5f * strength);
                    c.VX = dx / dist * kick;
                    c.VY = dy / dist * kick;
                }
            }
        }

        /// <summary>
        /// The world holds its breath: every critter hides (fades out and
        /// halts) for <paramref name="seconds"/>. A boss wave uses this as its
        /// tell — the arena falls silent before the threat arrives.
        /// </summary>
        public void HideAll(float seconds)
        {
            if (seconds <= 0f) return;
            for (int i = 0; i < MaxCritters; i++)
            {
                ref var c = ref _critters[i];
                if (!c.Active || c.Despawning) continue;
                c.HideTime = MathF.Max(c.HideTime, seconds);
                c.PanicTime = 0f;
                c.VX = 0f;
                c.VY = 0f;
            }
        }

        /// <summary>Ends a hold-breath (wave cleared): critters fade back in and resume.</summary>
        public void EndHide()
        {
            for (int i = 0; i < MaxCritters; i++)
            {
                ref var c = ref _critters[i];
                if (!c.Active) continue;
                c.HideTime = 0f;
                PickWanderTarget(ref c);
            }
        }

        /// <summary>Reads a pool slot (whether active or not).</summary>
        public Critter GetCritter(int index) => _critters[index];

        /// <summary>
        /// Bioluminescent flash intensity in [<see cref="FlashDimBase"/>, 1]
        /// for a cycling glow species: one short smooth flash per
        /// <see cref="FlashPeriod"/> seconds, a faint baseline the rest of the
        /// time. Real fireflies are dark most of the time — the darkness is
        /// what makes the glow read as alive instead of as a lamp (research:
        /// docs/external-research/living-world-wildlife-patterns.md).
        /// </summary>
        public static float FireflyFlash(float time, float phase01)
        {
            float cycles = time / FlashPeriod + phase01;
            float phase = cycles - MathF.Floor(cycles);
            if (phase >= FlashDuty) return FlashDimBase;

            float k = phase / FlashDuty;
            float envelope = k < 0.5f ? k * 2f : (1f - k) * 2f;
            return FlashDimBase + (1f - FlashDimBase) * envelope;
        }

        /// <summary>Number of living (non-despawning) critters, hidden ones included.</summary>
        public int LivingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxCritters; i++)
                {
                    if (_critters[i].Active && !_critters[i].Despawning) n++;
                }
                return n;
            }
        }

        private void UpdateSpawning(float dt)
        {
            if (_profiles.Length == 0) return;

            // Over target: fade out the farthest-from-hero critters first
            // (deterministic: ties broken by pool index).
            int living = LivingCount;
            while (living > _targetPopulation)
            {
                int worst = -1;
                float worstDist = -1f;
                for (int i = 0; i < MaxCritters; i++)
                {
                    ref var c = ref _critters[i];
                    if (!c.Active || c.Despawning) continue;
                    float dist = _heroSet
                        ? (c.X - _heroX) * (c.X - _heroX) + (c.Y - _heroY) * (c.Y - _heroY)
                        : i;
                    if (dist > worstDist)
                    {
                        worstDist = dist;
                        worst = i;
                    }
                }
                if (worst < 0) break;
                _critters[worst].Despawning = true;
                living--;
            }

            _spawnCooldown -= dt;
            if (living < _targetPopulation && _spawnCooldown <= 0f)
            {
                TrySpawn();
                _spawnCooldown = SpawnInterval;
            }
        }

        private void TrySpawn()
        {
            int slot = -1;
            for (int i = 0; i < MaxCritters; i++)
            {
                if (!_critters[i].Active)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) return;

            int species = PickSpecies();
            var pos = PickSpawnPosition();

            _critters[slot] = new Critter
            {
                Active = true,
                Despawning = false,
                X = pos.X,
                Y = pos.Y,
                HomeX = pos.X,
                HomeY = pos.Y,
                TargetX = pos.X,
                TargetY = pos.Y,
                Fade = 0f,
                Phase = (float)(_rng.NextDouble() * Math.PI * 2.0),
                Species = species,
            };
            ActiveCount = CountActive();
        }

        private int PickSpecies()
        {
            if (_profiles.Length == 1) return 0;
            // 70/30 split primary/secondary; keeps each biome's signature.
            return _rng.NextDouble() < 0.7 ? 0 : 1;
        }

        private (float X, float Y) PickSpawnPosition()
        {
            var (minX, minY, maxX, maxY) = ArenaBounds();
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float x = minX + (float)_rng.NextDouble() * (maxX - minX);
                float y = minY + (float)_rng.NextDouble() * (maxY - minY);
                if (!_heroSet) return (x, y);
                float dx = x - _heroX;
                float dy = y - _heroY;
                if (dx * dx + dy * dy >= HeroSpawnClearance * HeroSpawnClearance)
                    return (x, y);
            }
            return (_arenaCx + _arenaHalfW, _arenaCy + _arenaHalfH);
        }

        private void UpdateFade(ref Critter c, float dt)
        {
            float target = c.Despawning ? 0f : (c.HideTime > 0f ? 0f : 1f);
            float seconds = target > c.Fade ? FadeInSeconds : FadeOutSeconds;
            c.Fade = MoveTowards(c.Fade, target, dt / seconds);

            if (c.Despawning && c.Fade <= 0.001f)
            {
                c.Active = false;
                c.Despawning = false;
                ActiveCount = CountActive();
            }
        }

        private void Steer(int index, ref Critter c, float dt, float windX, float windY)
        {
            var spec = SpeciesAt(c.Species);
            float desiredX;
            float desiredY;
            float maxSpeed;

            if (c.PanicTime > 0f)
            {
                desiredX = c.X - c.PanicX;
                desiredY = c.Y - c.PanicY;
                Normalize(ref desiredX, ref desiredY, keepCurrent: (c.VX, c.VY));
                desiredX *= spec.PanicSpeed;
                desiredY *= spec.PanicSpeed;
                // Panicked creatures barely notice the wind.
                desiredX += windX * 0.1f;
                desiredY += windY * 0.1f;
                maxSpeed = spec.PanicSpeed;
            }
            else if (ShouldBeCurious(c, spec, out float towardX, out float towardY, out float dist))
            {
                float speed = spec.Speed * _curiosity;
                if (dist > CuriosityStandoff)
                {
                    desiredX = towardX * speed;
                    desiredY = towardY * speed;
                }
                else
                {
                    desiredX = -towardX * spec.Speed * 0.5f;
                    desiredY = -towardY * spec.Speed * 0.5f;
                }
                desiredX += windX * 0.3f;
                desiredY += windY * 0.3f;
                maxSpeed = spec.Speed * 1.5f;
            }
            else
            {
                c.WanderTimer -= dt;
                float tdx = c.TargetX - c.X;
                float tdy = c.TargetY - c.Y;
                float distSq = tdx * tdx + tdy * tdy;
                if (c.WanderTimer <= 0f || distSq < 14f * 14f)
                    PickWanderTarget(ref c);

                Normalize(ref tdx, ref tdy, keepCurrent: (c.VX, c.VY));
                desiredX = tdx * spec.Speed + windX * 0.3f;
                desiredY = tdy * spec.Speed + windY * 0.3f;
                maxSpeed = spec.Speed * 1.4f;
            }

            // Soft separation: critters are individual animals, not a swarm —
            // they push apart at contact but keep drifting (O(n²) over the
            // 64-slot pool is trivial at 60 Hz). Panic and hiding override
            // personal space: flight and breath-holding ignore it.
            if (c.PanicTime <= 0f)
            {
                float sepRadiusSq = SeparationRadius * SeparationRadius;
                for (int j = 0; j < MaxCritters; j++)
                {
                    if (j == index) continue;
                    var other = _critters[j];
                    if (!other.Active || other.Despawning || other.HideTime > 0f) continue;

                    float dx = c.X - other.X;
                    float dy = c.Y - other.Y;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= sepRadiusSq || d2 < 0.0001f) continue;

                    float d = MathF.Sqrt(d2);
                    float push = (1f - d / SeparationRadius) * SeparationWeight;
                    desiredX += dx / d * push;
                    desiredY += dy / d * push;
                }
            }

            // Exponential approach toward the desired velocity (smooth startles).
            float k = 1f - MathF.Exp(-6f * dt);
            c.VX += (desiredX - c.VX) * k;
            c.VY += (desiredY - c.VY) * k;
            var (cx, cy) = AmbientWindState.ClampSpeed(c.VX, c.VY, maxSpeed);
            c.VX = cx;
            c.VY = cy;
        }

        private bool ShouldBeCurious(Critter c, SpeciesProfile spec, out float towardX, out float towardY, out float dist)
        {
            towardX = 0f;
            towardY = 0f;
            dist = 0f;

            if (_curiosity <= 0.05f || !_heroSet) return false;
            if (spec.FleeSensitivity01 > CuriosityBoldnessThreshold) return false;

            float dx = _heroX - c.X;
            float dy = _heroY - c.Y;
            dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 0.001f) return false;
            towardX = dx / dist;
            towardY = dy / dist;
            return true;
        }

        private void PickWanderTarget(ref Critter c)
        {
            float tx = c.HomeX;
            float ty = c.HomeY; // fallback: stay home rather than walk onto scarred ground
            bool accepted = false;
            for (int attempt = 0; attempt < ScarAttempts && !accepted; attempt++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                float radius = (float)_rng.NextDouble() * WanderRadius;
                tx = c.HomeX + MathF.Cos(angle) * radius;
                ty = c.HomeY + MathF.Sin(angle) * radius;
                if (ScarSampler == null || ScarSampler(tx, ty) < ScarAvoidThreshold) accepted = true;
            }
            c.TargetX = tx;
            c.TargetY = ty;
            c.WanderTimer = 2.5f + (float)_rng.NextDouble() * 3.5f;
            ClampToArena(ref c.TargetX, ref c.TargetY);
        }

        private void Integrate(ref Critter c, float dt)
        {
            c.X += c.VX * dt;
            c.Y += c.VY * dt;

            float preX = c.X;
            float preY = c.Y;
            ClampToArena(ref c.X, ref c.Y);
            if (!MathfApproximately(c.X, preX) || !MathfApproximately(c.Y, preY))
            {
                // Hit a wall: reflect softly so critters turn back in.
                if (!MathfApproximately(c.X, preX)) c.VX = -c.VX * 0.4f;
                if (!MathfApproximately(c.Y, preY)) c.VY = -c.VY * 0.4f;
                PickWanderTarget(ref c);
            }
        }

        private void ClampToArena(ref float x, ref float y)
        {
            var (minX, minY, maxX, maxY) = ArenaBounds();
            x = Math.Clamp(x, minX, maxX);
            y = Math.Clamp(y, minY, maxY);
        }

        private (float MinX, float MinY, float MaxX, float MaxY) ArenaBounds()
        {
            float hw = _arenaHalfW - EdgeMargin;
            float hh = _arenaHalfH - EdgeMargin;
            if (hw < 16f) hw = 16f;
            if (hh < 16f) hh = 16f;
            return (_arenaCx - hw, _arenaCy - hh, _arenaCx + hw, _arenaCy + hh);
        }

        private SpeciesProfile SpeciesAt(int index)
        {
            if (_profiles.Length == 0) return default;
            if ((uint)index >= (uint)_profiles.Length) return _profiles[0];
            return _profiles[index];
        }

        private int CountActive()
        {
            int n = 0;
            for (int i = 0; i < MaxCritters; i++)
            {
                if (_critters[i].Active) n++;
            }
            return n;
        }

        private static void Normalize(ref float x, ref float y, (float X, float Y) keepCurrent)
        {
            float lenSq = x * x + y * y;
            if (lenSq < 0.0001f)
            {
                x = keepCurrent.X;
                y = keepCurrent.Y;
                return;
            }
            float len = MathF.Sqrt(lenSq);
            x /= len;
            y /= len;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (MathF.Abs(target - current) <= maxDelta) return target;
            return current + MathF.Sign(target - current) * maxDelta;
        }

        private static bool MathfApproximately(float a, float b) => MathF.Abs(a - b) <= 1e-4f;
    }
}
