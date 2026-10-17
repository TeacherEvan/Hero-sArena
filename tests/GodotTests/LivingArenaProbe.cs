using Godot;
using System;

namespace HeroArena.Tests
{
    /// <summary>
    /// Godot headless probe for the Living Arena nodes (AmbientWildlife,
    /// BiomeAtmosphere, LivingGround). Run via the scene entry:
    ///
    ///   env DOTNET_ROOT=~/.dotnet8 ~/.local/bin/godot-mono --headless --path . \
    ///       res://tests/GodotTests/LivingArenaProbe.tscn
    ///
    /// Prints PASS/FAIL lines and exits non-zero on failure. Checks: Main.tscn
    /// wiring + draw order, arena-resolver centring, wildlife boot + spawning,
    /// the boss hold-breath mood machine, atmosphere fields + gradient
    /// textures, and ground boot. Each node is booted standalone on the live
    /// SceneTree and driven through deterministic _Process ticks, so spawning,
    /// mood transitions, wind sampling and event subscription paths are
    /// executed by the real engine — not re-simulated here. _Draw output
    /// cannot be asserted headlessly; the node's geometry math is covered by
    /// the GroundDetailPlan xUnit suite, and a 600-frame Main.tscn run with
    /// zero script errors is the end-to-end frame evidence (see the
    /// investigation note for this pass).
    /// </summary>
    [GlobalClass]
    public partial class LivingArenaProbe : Node
    {
        public override void _Ready()
        {
            var failed = 0;
            var passed = 0;

            try { TestMainSceneWiring(); passed++; GD.Print("PASS: Main.tscn living-arena wiring"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: Main.tscn wiring - {e.Message}"); }

            try { TestArenaResolver(); passed++; GD.Print("PASS: arena resolver centres on the map origin"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: arena resolver - {e.Message}"); }

            try { TestScarLayer(); passed++; GD.Print("PASS: ScarLayer boot + blast scarring"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: ScarLayer - {e.Message}"); }

            try { TestWildlifeBootAndSpawn(); passed++; GD.Print("PASS: AmbientWildlife boot + spawn"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: AmbientWildlife boot - {e.Message}"); }

            try { TestWildlifeBossHold(); passed++; GD.Print("PASS: AmbientWildlife boss hold-breath"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: AmbientWildlife boss hold - {e.Message}"); }

            try { TestAtmosphereBoot(); passed++; GD.Print("PASS: BiomeAtmosphere boot"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: BiomeAtmosphere boot - {e.Message}"); }

            try { TestGroundBoot(); passed++; GD.Print("PASS: LivingGround boot"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: LivingGround boot - {e.Message}"); }

            GD.Print($"\n=== Living Arena probe: {passed} passed, {failed} failed ===");
            GetTree().Quit(failed > 0 ? 1 : 0);
        }

        /// <summary>Main.tscn must wire all three nodes, in the documented draw order.</summary>
        private static void TestMainSceneWiring()
        {
            var packed = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn")
                ?? throw new Exception("Main.tscn failed to load");
            var root = packed.Instantiate();
            try
            {
                int ground = IndexOfChild(root, "LivingGround");
                int scar = IndexOfChild(root, "ScarLayer");
                int atmosphere = IndexOfChild(root, "BiomeAtmosphere");
                int wildlife = IndexOfChild(root, "AmbientWildlife");

                if (!(root.GetNodeOrNull("LivingGround") is LivingGround))
                    throw new Exception("LivingGround missing or bound to the wrong script");
                if (!(root.GetNodeOrNull("ScarLayer") is ScarLayer))
                    throw new Exception("ScarLayer missing or bound to the wrong script");
                if (!(root.GetNodeOrNull("BiomeAtmosphere") is BiomeAtmosphere))
                    throw new Exception("BiomeAtmosphere missing or bound to the wrong script");
                if (!(root.GetNodeOrNull("AmbientWildlife") is AmbientWildlife))
                    throw new Exception("AmbientWildlife missing or bound to the wrong script");

                if (!(ground < scar && scar < atmosphere && atmosphere < wildlife))
                    throw new Exception($"draw order wrong: LivingGround({ground}) < ScarLayer({scar}) < BiomeAtmosphere({atmosphere}) < AmbientWildlife({wildlife}) expected");
            }
            finally
            {
                root.Free();
            }
        }

        /// <summary>
        /// The shared arena resolver must read MapBase.ArenaSize as centred on
        /// the map node's origin (the hero spawns at world zero; map markers
        /// sit at ±800). This pins the fix for the audit's critical finding —
        /// a top-left interpretation had put the whole wildlife box and half
        /// the rain off-screen.
        /// </summary>
        private void TestArenaResolver()
        {
            var packed = ResourceLoader.Load<PackedScene>("res://scenes/maps/RuinedMetropolis.tscn")
                ?? throw new Exception("RuinedMetropolis.tscn failed to load");
            var map = packed.Instantiate<MapBase>();
            map.Name = "Map";
            map.Position = new Vector2(50f, -30f);
            map.ArenaSize = new Vector2(3000f, 2000f);
            AddChild(map);
            try
            {
                var (center, half) = SceneAnchors.ResolveArena(this);
                if (!center.IsEqualApprox(new Vector2(50f, -30f)))
                    throw new Exception($"centre should be the map origin (50,-30), got {center}");
                if (!half.IsEqualApprox(new Vector2(1500f, 1000f)))
                    throw new Exception($"half extents should be ArenaSize/2 (1500,1000), got {half}");
            }
            finally
            {
                RemoveChild(map);
                map.Free();
            }
        }

        /// <summary>
        /// Blast events stamp scars into the memory and the sampler reports
        /// them — the write path of R-LA6, exercised on the live tree.
        /// </summary>
        private void TestScarLayer()
        {
            var scars = new ScarLayer();
            AddChild(scars);
            try
            {
                EventBus.Instance.EmitEnvironmentDestroyed(Vector2.Zero, 40f);
                Tick(scars, 72); // 1.2 s: fade-in complete
                if (scars.SampleScar(0f, 0f) <= 0.1f)
                    throw new Exception($"scar not sampled at blast centre: {scars.SampleScar(0f, 0f)}");
                if (scars.SampleScar(500f, 500f) != 0f)
                    throw new Exception($"scar field leaked outside the blast radius");
            }
            finally
            {
                scars.QueueFree();
            }
        }

        /// <summary>The pool must boot and critters must fade in over the spawn schedule.</summary>
        private void TestWildlifeBootAndSpawn()
        {
            var wildlife = new AmbientWildlife();
            AddChild(wildlife);

            int sprites = CountSprites(wildlife, visibleOnly: false);
            if (sprites != 128)
                throw new Exception($"expected 64 body + 64 glow sprites (128), got {sprites}");

            Tick(wildlife, 240); // 4 s at 60 Hz: spawns every 0.35 s, fade-in 1.6 s

            int visible = CountSprites(wildlife, visibleOnly: true);
            if (visible < 8)
                throw new Exception($"expected spawned critters to be visible after 4 s, got {visible} visible sprites");

            wildlife.QueueFree();
        }

        /// <summary>
        /// Boss incoming must empty the arena (hold breath), and wave completion
        /// must let the wildlife fade back in.
        /// </summary>
        private void TestWildlifeBossHold()
        {
            var wildlife = new AmbientWildlife();
            AddChild(wildlife);
            Tick(wildlife, 240);

            if (CountSprites(wildlife, visibleOnly: true) < 8)
                throw new Exception("precondition failed: no visible critters before the boss hold");

            EventBus.Instance.EmitWaveIncoming(3, isBoss: true);
            EventBus.Instance.EmitWaveStarted(3);
            Tick(wildlife, 180); // 3 s: hide fade-out (1.2 s) + population-0 despawn

            int hiding = CountSprites(wildlife, visibleOnly: true);
            if (hiding != 0)
                throw new Exception($"boss hold should empty the arena, got {hiding} visible sprites");

            EventBus.Instance.EmitWaveCompleted(3);
            Tick(wildlife, 150); // 2.5 s: population restored, fresh spawns fade in

            if (CountSprites(wildlife, visibleOnly: true) == 0)
                throw new Exception("wildlife did not return after the boss wave completed");

            wildlife.QueueFree();
        }

        /// <summary>
        /// Two code-generated fields must boot emitting, biome-resolved, and
        /// carry a visible texture — including the mid-peak rain streak whose
        /// gradient the audit found collapsed to fully transparent.
        /// </summary>
        private void TestAtmosphereBoot()
        {
            var atmosphere = new BiomeAtmosphere();
            AddChild(atmosphere);

            int fields = 0;
            float amountSum = 0f;
            foreach (var child in atmosphere.GetChildren())
            {
                if (child is not CpuParticles2D particles) continue;
                fields++;
                amountSum += particles.Amount;
                if (!particles.Emitting)
                    throw new Exception("a biome field booted not emitting");

                var gradient = (particles.Texture as GradientTexture2D)?.Gradient
                    ?? throw new Exception($"{particles.Name} has no gradient texture");
                float brightest = 0f;
                foreach (float t in new[] { 0.02f, 0.25f, 0.5f, 0.75f, 0.98f })
                    brightest = MathF.Max(brightest, gradient.Sample(t).A);
                if (brightest < 0.25f)
                    throw new Exception($"{particles.Name} texture is invisible (max sampled alpha {brightest})");
            }
            if (fields != 2)
                throw new Exception($"expected 2 particle fields, got {fields}");

            // A Map-less probe scene resolves to the default biome (Ruined
            // Metropolis): 90 rain + 30 haze. Pins path→biome fallback.
            if (!Mathf.IsEqualApprox(amountSum, 120f))
                throw new Exception($"unexpected default biome field budget: {amountSum}");

            // The rain streak must brighten toward its middle: transparent
            // endpoints on both sides would make the signature field invisible.
            var rainKind = BiomeAtmosphereState.ProfileFor(BiomeKind.RuinedMetropolis).PrimaryField.Kind;
            var rain = atmosphere.GetNodeOrNull<CpuParticles2D>($"Field_{rainKind}")
                ?? throw new Exception($"missing primary field node Field_{rainKind}");
            var rainGradient = (rain.Texture as GradientTexture2D)!.Gradient;
            float edgeA = rainGradient.Sample(0.02f).A;
            float edgeB = rainGradient.Sample(0.98f).A;
            float midA = rainGradient.Sample(0.5f).A;
            if (midA < 0.6f || midA < edgeA + 0.3f || midA < edgeB + 0.3f)
                throw new Exception($"rain streak must peak mid-line (edges {edgeA}/{edgeB}, mid {midA})");

            Tick(atmosphere, 30); // wind slants gravity; must not throw
        }

        /// <summary>Ground boots (single _Draw node, no children) and its region pass survives ticks.</summary>
        private void TestGroundBoot()
        {
            var ground = new LivingGround();
            AddChild(ground);
            Tick(ground, 10);
            if (ground.GetChildCount() != 0)
                throw new Exception("LivingGround should draw without child nodes");
            ground.QueueFree();
        }

        private static void Tick(Node node, int frames)
        {
            for (int i = 0; i < frames; i++)
                node._Process(1.0 / 60.0);
        }

        private static int CountSprites(Node node, bool visibleOnly)
        {
            int count = 0;
            foreach (var child in node.GetChildren())
            {
                if (child is not Sprite2D sprite) continue;
                if (visibleOnly && !sprite.Visible) continue;
                count++;
            }
            return count;
        }

        private static int IndexOfChild(Node root, string name)
        {
            for (int i = 0; i < root.GetChildCount(); i++)
            {
                if (root.GetChild(i).Name == name) return i;
            }
            return -1;
        }
    }
}
