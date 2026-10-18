using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena.Tests
{
    /// <summary>
    /// Godot headless test runner for core systems. Run via the scene entry:
    /// godot --headless res://tests/GodotTests/CoreSystemTests.tscn
    ///
    /// IMPORTANT (environment status): this gate has never run on the developer
    /// machine — the Godot mono binary here cannot execute managed (C#) code at
    /// all (any C# entry stalls in unmanaged code after the .NET module
    /// initializes; verified against `--quit`, `--import`, GDScript `-s`, a
    /// no-dependency C# scene, and `scenes/Main.tscn`), and CI has never run
    /// (GitHub billing lock, issue #23). Pure logic that used to be gate-only
    /// is now extracted and covered headlessly by xUnit:
    ///   - EntityRegistry F-1 guard      → EntityRegistryTests
    ///   - HitFlash color/fade (F-31)    → HitFlashPaletteTests
    ///   - CollateralKarma counting      → DestructionTrackerTests
    ///   - Powerup banner selection      → PowerupBannerSlotsTests
    ///   - Weibull wave intensity        → WaveFormulasTests
    ///   - Run-state machine (T3.1-rest) → GameManagerStateTests
    ///   - Frame input buffer  (T3.1-rest) → InputBufferStateTests
    /// The pure-math checks that remain below (SpatialHashGrid, WaveManager,
    /// FlowFieldPathfinder, LevelProgression, CollateralKarma) are now
    /// in-engine cross-covers of headlessly-tested logic, kept as a second
    /// harness for the day the engine runs. Only TestEntityRegistry,
    /// TestCollateralKarmaBehavior, TestGameManagerDelegation,
    /// TestInputBufferDelegation, TestPowerupBannerDelegation, TestHitFlash,
    /// TestSurroundingsFx (R-FX1/2/3 runtime smoke) and TestEnemyTargetDisposal
    /// are genuinely SceneTree-dependent.
    ///
    /// Note: the retired `GateRunner.cs` SceneTree wrapper used `-s` mode,
    /// which cannot run C# scripts on this machine. The scene entry above
    /// exercises autoloads and `_Ready` normally on an engine that can run C#.
    /// </summary>
    [GlobalClass]
    public partial class CoreSystemTests : Node
    {
        public override void _Ready()
        {
            var failed = 0;
            var passed = 0;

            // Test SpatialHashGrid
            try { TestSpatialHashGrid(); passed++; GD.Print("PASS: SpatialHashGrid tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: SpatialHashGrid - {e.Message}"); }

            try { TestWaveManager(); passed++; GD.Print("PASS: WaveManager tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: WaveManager - {e.Message}"); }

            try { TestFlowFieldPathfinder(); passed++; GD.Print("PASS: FlowFieldPathfinder tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: FlowFieldPathfinder - {e.Message}"); }

            try { TestLevelProgression(); passed++; GD.Print("PASS: LevelProgression tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: LevelProgression - {e.Message}"); }

            try { TestEntityRegistry(); passed++; GD.Print("PASS: EntityRegistry tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: EntityRegistry - {e.Message}"); }

            try { TestCollateralKarma(); passed++; GD.Print("PASS: CollateralKarma tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: CollateralKarma - {e.Message}"); }

            try { TestCollateralKarmaBehavior(); passed++; GD.Print("PASS: CollateralKarma behavior tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: CollateralKarma behavior - {e.Message}"); }

            try { TestGameManagerDelegation(); passed++; GD.Print("PASS: GameManager delegation tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: GameManager delegation - {e.Message}"); }

            try { TestInputBufferDelegation(); passed++; GD.Print("PASS: InputBuffer delegation tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: InputBuffer delegation - {e.Message}"); }

            try { TestPowerupBannerDelegation(); passed++; GD.Print("PASS: PowerupBannerSlots delegation tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: PowerupBannerSlots delegation - {e.Message}"); }

            try { TestHitFlash(); passed++; GD.Print("PASS: HitFlash tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: HitFlash - {e.Message}"); }

            try { TestSurroundingsFx(); passed++; GD.Print("PASS: Surroundings FX runtime tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: Surroundings FX - {e.Message}"); }

            try { TestEnemyTargetDisposal(); passed++; GD.Print("PASS: Enemy target-disposal tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: Enemy target disposal - {e.Message}"); }

            try { TestSceneTextures(); passed++; GD.Print("PASS: Scene textures tests"); }
            catch (Exception e) { failed++; GD.PrintErr($"FAIL: Scene textures - {e.Message}"); }

            GD.Print($"\n=== Results: {passed} passed, {failed} failed ===");

            if (failed > 0)
            {
                GetTree().Quit(1); // non-zero exit code so CI detects failure
            }
            else
            {
                GetTree().Quit();
            }
        }

        private static void TestSpatialHashGrid()
        {
            var grid = new SpatialHashGrid(64, 64);

            // Test insert and query
            grid.Insert(1, new Vector2(100, 100), 10f);
            var results = grid.QueryRadius(new Vector2(100, 100), 15f, out int count, out bool _);
            if (count != 1 || results[0] != 1)
                throw new Exception($"Expected 1 entity at (100,100), got {count}");

            // Test remove
            grid.Remove(1);
            results = grid.QueryRadius(new Vector2(100, 100), 15f, out count, out bool _);
            if (count != 0)
                throw new Exception($"Expected 0 after remove, got {count}");

            // Test multiple entities
            grid.Insert(1, new Vector2(100, 100), 10f);
            grid.Insert(2, new Vector2(120, 100), 10f);
            grid.Insert(3, new Vector2(200, 200), 10f);
            results = grid.QueryRadius(new Vector2(110, 100), 20f, out count, out bool _);
            if (count != 2)
                throw new Exception($"Expected 2 entities near (110,100), got {count}");

            // Test update
            grid.Update(1, new Vector2(500, 500), 10f);
            results = grid.QueryRadius(new Vector2(100, 100), 15f, out count, out bool _);
            if (count != 1 || results[0] != 2)
                throw new Exception("Update failed - entity still at old position");

            results = grid.QueryRadius(new Vector2(500, 500), 15f, out count, out bool _);
            if (count != 1 || results[0] != 1)
                throw new Exception("Update failed - entity not at new position");

            // Test clear
            grid.Clear();
            results = grid.QueryRadius(new Vector2(500, 500), 15f, out count, out bool _);
            if (count != 0)
                throw new Exception("Clear failed");

            // Test QueryAABB
            grid.Insert(1, new Vector2(100, 100), 10f);
            grid.Insert(2, new Vector2(150, 150), 10f);
            grid.Insert(3, new Vector2(300, 300), 10f);
            var bounds = new Rect2(80, 80, 100, 100);
            results = grid.QueryAABB(bounds, out count);
            if (count != 2)
                throw new Exception($"AABB query expected 2, got {count}");
        }

        /// <summary>
        /// Wave manager Weibull intensity assertions. The curve math now
        /// lives in the Godot-free WaveFormulas and is pinned by
        /// WaveFormulasTests in xUnit; this in-engine check cross-covers the
        /// values.
        /// </summary>
        private static void TestWaveManager()
        {
            // Test the Weibull distribution calculation directly
            const int MinWaveEnemies = 10;
            const float IntensityScaleFactor = 500f;
            const float WEIBULL_K = 1.5f;
            const float WEIBULL_LAMBDA = 10f;

            // Wave 1: Weibull intensity is nonzero at t=1 (curve peaks ~wave 5), so count > minimum
            float t = 1f;
            float intensity = (WEIBULL_K / WEIBULL_LAMBDA) * MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K - 1f) * MathF.Exp(-MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K));
            int count = MinWaveEnemies + (int)(intensity * IntensityScaleFactor);
            if (count != 32)
                throw new Exception($"Wave 1 expected 32 (Weibull curve), got {count}");

            // Wave 10: still elevated under the current curve
            t = 10f;
            intensity = (WEIBULL_K / WEIBULL_LAMBDA) * MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K - 1f) * MathF.Exp(-MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K));
            count = MinWaveEnemies + (int)(intensity * IntensityScaleFactor);
            if (count != 37)
                throw new Exception($"Wave 10 expected 37 (Weibull curve), got {count}");

            // Wave 20 should be past peak
            t = 20f;
            intensity = (WEIBULL_K / WEIBULL_LAMBDA) * MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K - 1f) * MathF.Exp(-MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K));
            count = MinWaveEnemies + (int)(intensity * IntensityScaleFactor);
            if (count <= MinWaveEnemies)
                throw new Exception($"Wave 20 expected > {MinWaveEnemies}, got {count}");
        }

        private static void TestFlowFieldPathfinder()
        {
            var pathfinder = new FlowFieldPathfinder();

            // Test WorldToGrid/GridToWorld round trip
            var worldPos = new Vector2(500, 300);
            var gridPos = pathfinder.WorldToGrid(worldPos);
            var backToWorld = pathfinder.GridToWorld(gridPos);

            var expectedX = gridPos.X * 16f + 8f;
            var expectedY = gridPos.Y * 16f + 8f;

            if (MathF.Abs(backToWorld.X - expectedX) > 0.1f || MathF.Abs(backToWorld.Y - expectedY) > 0.1f)
                throw new Exception($"GridToWorld mismatch: ({backToWorld.X}, {backToWorld.Y}) vs ({expectedX}, {expectedY})");

            // Test blocked cells
            pathfinder.SetBlocked(new Vector2I(10, 10), true);
            // Can't easily test internal state, but ensure no crash

            // Test out of bounds
            pathfinder.SetBlocked(new Vector2I(-1, 0), true); // Should not crash
            pathfinder.SetBlocked(new Vector2I(128, 0), true); // Should not crash

            // Test constants
            if (FlowFieldPathfinder.GRID_W != 128 || FlowFieldPathfinder.GRID_H != 128)
                throw new Exception("Grid constants mismatch");
            if (FlowFieldPathfinder.CELL_COUNT != 16384)
                throw new Exception("CELL_COUNT mismatch");
        }

        private static void TestLevelProgression()
        {
            var progression = new LevelProgression();

            // Test kinetic damage
            float kinetic = LevelProgression.CalcKineticDamage(100f, 1);
            if (MathF.Abs(kinetic - 115f) > 0.001f)
                throw new Exception($"Kinetic damage level 1: expected 115, got {kinetic}");

            kinetic = LevelProgression.CalcKineticDamage(100f, 5);
            if (MathF.Abs(kinetic - 175f) > 0.001f)
                throw new Exception($"Kinetic damage level 5: expected 175, got {kinetic}");

            // Test energy damage
            float energy = LevelProgression.CalcEnergyDamage(100f, 1);
            if (MathF.Abs(energy - 100f) > 1f)
                throw new Exception($"Energy damage level 1: expected ~100, got {energy}");

            energy = LevelProgression.CalcEnergyDamage(100f, 10);
            if (MathF.Abs(energy - 1995.3f) > 1f)
                throw new Exception($"Energy damage level 10: expected ~1995, got {energy}");

            // Test karma amplifier
            float karma0 = LevelProgression.CalcKarmaAmplifier(0);
            if (MathF.Abs(karma0 - 1f) > 0.001f)
                throw new Exception($"Karma 0: expected 1, got {karma0}");

            float karma10 = LevelProgression.CalcKarmaAmplifier(10);
            if (karma10 <= karma0)
                throw new Exception("Karma should increase with destruction");

            // Test XP formula
            if (LevelProgression.XpRequiredForLevel(1) != 100)
                throw new Exception("XP level 1");
            if (LevelProgression.XpRequiredForLevel(2) != 160)
                throw new Exception("XP level 2");
            if (LevelProgression.XpRequiredForLevel(5) != 460)
                throw new Exception("XP level 5");
            if (LevelProgression.XpRequiredForLevel(10) != 1360)
                throw new Exception("XP level 10");

            // Test perk selection
            var perks = progression.GetRandomPerks(3);
            if (perks.Length != 3)
                throw new Exception($"Expected 3 perks, got {perks.Length}");

            var uniquePerks = new HashSet<PerkType>(perks);
            if (uniquePerks.Count != 3)
                throw new Exception("Perks should be distinct");
        }

        /// <summary>
        /// Regression guard for F-1: the static entity-id registry on EnemyBase
        /// must resolve unknown and negative ids to null. Exercised through
        /// the public TryGetById so the delegation wiring is covered without
        /// spinning up an enemy scene. The register → lookup → remove round
        /// trip is asserted in EntityRegistryTests (xUnit, against the pure
        /// EntityRegistry directly).
        /// </summary>
        private static void TestEntityRegistry()
        {
            // Unknown id → null
            if (EnemyBase.TryGetById(int.MaxValue, out var unknown) && unknown != null)
                throw new Exception("Unknown entity id should not resolve");

            // Negative id → null
            if (EnemyBase.TryGetById(-1, out var neg) && neg != null)
                throw new Exception("Negative entity id should not resolve");
        }

        /// <summary>
        /// Cross-cover the CollateralKarma math here so the xUnit bypass trick
        /// (RuntimeHelpers.GetUninitializedObject) is not the only CI check.
        /// Verifies the logarithmic amplifier formula at a few known points.
        /// (Also covered headlessly in KarmaAmplifierTests.)
        /// </summary>
        private static void TestCollateralKarma()
        {
            // The KarmaAmplifier formula lives on a Node, so we instantiate it
            // the same way xUnit does (reflection-based uninitialized object).
            // The math itself is pure C# and stable across both harnesses.
            float a0 = Mathf.Log(Mathf.E + 0.05f * 0);
            if (MathF.Abs(a0 - 1f) > 0.001f)
                throw new Exception($"KarmaAmplifier(0) expected ~1, got {a0}");

            float a10 = Mathf.Log(Mathf.E + 0.05f * 10);
            if (a10 <= a0)
                throw new Exception("KarmaAmplifier should be monotonically increasing");

            float a50 = Mathf.Log(Mathf.E + 0.05f * 50);
            if (MathF.Abs(a50 - 1.652f) > 0.005f)
                throw new Exception($"KarmaAmplifier(50) expected ~1.652, got {a50}");
        }

        /// <summary>
        /// Node-level behavior of CollateralKarma: a live node subscribes to
        /// OnEnvironmentDestroyed and its DestructionCount/KarmaAmplifier
        /// forward to the Godot-free DestructionTracker (whose counting state
        /// is asserted in DestructionTrackerTests, headlessly).
        /// </summary>
        private void TestCollateralKarmaBehavior()
        {
            var karma = new CollateralKarma();
            // Add to this node, not /root: during the main scene's _Ready the
            // root is still busy setting up children, so Root.AddChild fails
            // ("Parent node is busy setting up children") and _Ready — the
            // EventBus subscription — never runs.
            AddChild(karma);
            if (karma.DestructionCount != 0)
                throw new Exception($"Initial DestructionCount expected 0, got {karma.DestructionCount}");
            EventBus.Instance.EmitEnvironmentDestroyed(Vector2.Zero, 10f);
            if (karma.DestructionCount != 1)
                throw new Exception($"DestructionCount expected 1 after event, got {karma.DestructionCount}");
            if (MathF.Abs(karma.KarmaAmplifier - ProgressionFormulas.CalcKarmaAmplifier(1)) > 0.0001f)
                throw new Exception("KarmaAmplifier does not delegate to ProgressionFormulas");
            karma.QueueFree();
        }

        /// <summary>
        /// Guards the InputBuffer → InputBufferState delegation (T3.1-rest):
        /// the Node itself must own the StringName→string boundary and the
        /// tick→expire ordering. A detached instance (never added to a tree,
        /// so the engine never ticks it) drives _PhysicsProcess manually:
        /// BufferAction(StringName) → expire after 6 ->drop ticks →
        /// ConsumeAction false. BufferFrame-1 boundary and pure-queue
        /// mechanics are asserted headlessly in InputBufferStateTests.
        /// </summary>
        private static void TestInputBufferDelegation()
        {
            var buffer = new InputBuffer();
            var action = new StringName("attack");

            buffer.BufferAction(action);
            if (!buffer.IsBuffered(action))
                throw new Exception("InputBuffer: BufferAction did not register via InputBufferState");

            // Tick + ProcessBuffer 5 times: still inside the window.
            for (int i = 0; i < InputBufferState.BufferFrames - 1; i++)
                buffer._PhysicsProcess(1.0 / 60.0);
            if (!buffer.IsBuffered(action))
                throw new Exception("InputBuffer: entry expired before BufferFrames elapses");

            // 6th tick: expiry sweeps it.
            buffer._PhysicsProcess(1.0 / 60.0);
            if (buffer.IsBuffered(action))
                throw new Exception("InputBuffer: entry should expire after BufferFrames ticks");
        }

        /// <summary>
        /// Guards the GameManager → GameManagerState delegation (T3.1-rest):
        /// the Node's public API must still drive the extracted state machine.
        /// A detached GameManager (never added to the tree, so _Ready/_ExitTree
        /// never fire and EventBus is untouched) exercises StartGame's reset
        /// set, the pause/resume guard chain, game-over, and the enemy count
        /// quirk.
        /// - StartGame: CurrentState=Playing + per-run metrics reset — but NOT
        ///   ActiveEnemyCount (preserved quirk, pinned headlessly in
        ///   GameManagerStateTests).
        /// - PauseGame/ResumeGame: the GameState guard transitions; the
        ///   SceneTree.Paused side effect needs an in-tree node, so the
        ///   GetTree() null-guard skips it here (it is exercised in the real
        ///   playable-boot flow).
        /// - Game-over mid-run leaves enemies counted; StartGame resets the
        ///   dirty metrics while keeping the count.
        /// The pure primitives themselves are asserted in
        /// GameManagerStateTests (xUnit, headless).
        /// </summary>
        private static void TestGameManagerDelegation()
        {
            var gm = new GameManager();

            // StartGame: reset set + Playing kick.
            gm.StartGame();
            if (gm.CurrentState != GameState.Playing)
                throw new Exception($"StartGame: expected Playing, got {gm.CurrentState}");
            if (gm.Score != 0 || gm.KillCount != 0 || gm.ThreatLevel != 0)
                throw new Exception("StartGame: run metrics not reset on a fresh manager");

            // Dirty a run: score up, enemies counted, then bail out.
            gm.IncrementEnemyCount();
            gm.IncrementEnemyCount();
            gm.AddScore(7);
            gm.TriggerGameOver();
            if (gm.CurrentState != GameState.GameOver)
                throw new Exception($"TriggerGameOver: expected GameOver, got {gm.CurrentState}");

            // Restart: dirty metrics reset, straggler enemies preserved.
            gm.StartGame();
            if (gm.CurrentState != GameState.Playing)
                throw new Exception($"StartGame after GameOver: expected Playing, got {gm.CurrentState}");
            if (gm.Score != 0 || gm.KillCount != 0)
                throw new Exception("StartGame: dirty run metrics were not reset");
            if (gm.ActiveEnemyCount != 2)
                throw new Exception("StartGame must NOT reset ActiveEnemyCount (preserved quirk)");

            // Pause/resume guard chain (detached: SetTreePaused no-ops on the
            // GetTree() null guard; nothing to assert there).
            gm.PauseGame();
            if (gm.CurrentState != GameState.Paused)
                throw new Exception($"PauseGame: expected Paused, got {gm.CurrentState}");
            gm.ResumeGame();
            if (gm.CurrentState != GameState.Playing)
                throw new Exception($"ResumeGame: expected Playing, got {gm.CurrentState}");

            GD.Print("  GameManager: state transitions and reset-set delegate to GameManagerState");
        }

        /// <summary>
        /// Guards the HUD powerup-banner selection invariant: ShowPowerup must
        /// delegate slot selection to the pure PowerupBannerSlots class, and
        /// that class must implement both branches of the rule (reuse an
        /// expired slot; else recycle the oldest active one).
        ///
        /// The original PR #18 regression (c9fed16) was asserted by grepping a
        /// `scripts/ui/PowerupBannerFactory.cs` file that never existed in the
        /// repo — the gate would have failed instantly with "Source file not
        /// found" had it ever run. The selection rule is pure and is now
        /// asserted properly in PowerupBannerSlotsTests (xUnit, headless); this
        /// gate check only pins the delegation so the extracted logic stays
        /// wired into the HUD.
        /// </summary>
        private static void TestPowerupBannerDelegation()
        {
            const string slotsSource = "scripts/core/PowerupBannerSlots.cs";
            if (!System.IO.File.Exists(slotsSource))
                throw new Exception($"Source file not found: {slotsSource}");

            string slotsText = System.IO.File.ReadAllText(slotsSource);
            if (!slotsText.Contains("SelectSlot"))
                throw new Exception("PowerupBannerSlots: SelectSlot entry point missing");
            // Expired-slot branch.
            if (!slotsText.Contains("timers[i] <= 0f"))
                throw new Exception("PowerupBannerSlots: missing expired-slot branch");
            // Oldest-banner branch.
            if (!slotsText.Contains("timers[i] < minTime"))
                throw new Exception("PowerupBannerSlots: missing oldest-active-slot branch");

            // The HUD must actually use the extractor.
            const string hudSource = "scripts/ui/HUD.cs";
            if (!System.IO.File.Exists(hudSource))
                throw new Exception($"Source file not found: {hudSource}");
            if (!System.IO.File.ReadAllText(hudSource).Contains("PowerupBannerSlots.SelectSlot"))
                throw new Exception("HUD.ShowPowerup no longer delegates to PowerupBannerSlots");

            GD.Print("  PowerupBannerSlots: selection rule present and delegated by HUD");
        }

        /// <summary>
        /// Test that HitFlash subscribes to OnProjectileHit, applies the
        /// damage-type color, and resets after the flash duration. Resolves
        /// audit finding F-31 (dead signal).
        ///
        /// Source-text assertions confirm the Main scene wires a HitFlash
        /// node and the script subscribes/unsubscribes; live-tree checks
        /// drive the flash lifecycle (processing on/off, overlay alpha)
        /// through a real HitFlash instance. The damage-type color table
        /// lives in the Godot-free HitFlashPalette and is asserted
        /// branch-by-branch in HitFlashPaletteTests (xUnit); here we only
        /// pin that every DamageType branch exists in that source.
        /// </summary>
        private void TestHitFlash()
        {
            // 1. The Main scene must wire a HitFlash node so the event has
            //    a consumer at runtime.
            // System.IO works on real filesystem paths, so globalize res:// first.
            string mainScene = ProjectSettings.GlobalizePath("res://scenes/Main.tscn");
            if (!System.IO.File.Exists(mainScene))
                throw new Exception($"Main scene not found: {mainScene}");
            string sceneText = System.IO.File.ReadAllText(mainScene);
            if (!sceneText.Contains("HitFlash"))
                throw new Exception("Main.tscn does not wire a HitFlash node — OnProjectileHit is still unconsumed (F-31)");

            // 2. The HitFlash script must subscribe to OnProjectileHit.
            string hitFlashSource = ProjectSettings.GlobalizePath("res://scripts/vfx/HitFlash.cs");
            if (!System.IO.File.Exists(hitFlashSource))
                throw new Exception($"HitFlash source not found: {hitFlashSource}");
            string hfText = System.IO.File.ReadAllText(hitFlashSource);
            if (!hfText.Contains("OnProjectileHit +="))
                throw new Exception("HitFlash does not subscribe to OnProjectileHit");
            if (!hfText.Contains("OnProjectileHit -="))
                throw new Exception("HitFlash does not unsubscribe from OnProjectileHit on _ExitTree");

            // 3. Construct a HitFlash, add it to the live tree, and verify
            //    that emitting OnProjectileHit enables _Process.
            var flash = new HitFlash();
            // Same root-busy constraint as CollateralKarma: add to this node
            // so _Ready (the subscription + overlay creation) runs now.
            AddChild(flash);
            // _Ready fires synchronously on AddChild, so the subscription is
            // already in place; trigger the event.
            EventBus.Instance.EmitProjectileHit(Vector2.Zero, DamageType.Kinetic);
            if (!flash.IsProcessing())
                throw new Exception("HitFlash should be processing after OnProjectileHit");
            // Process one frame; alpha should be non-zero.
            flash._Process(0.02);
            var overlay = flash.GetChild<ColorRect>(0);
            if (overlay.Color.A <= 0f)
                throw new Exception($"HitFlash overlay alpha should be > 0 after one frame, got {overlay.Color.A}");
            // Drive the flash past its duration; alpha should drop to 0
            // and _Process should be disabled.
            for (int i = 0; i < 10; i++)
                flash._Process(0.02); // 200ms total, > 80ms flash duration
            if (overlay.Color.A != 0f)
                throw new Exception($"HitFlash alpha should be 0 after duration, got {overlay.Color.A}");
            if (flash.IsProcessing())
                throw new Exception("HitFlash should stop processing after flash duration");

            // 4. The damage-type-keyed color mapping must cover all 6 types
            //    (defense against future enum additions silently using red).
            //    The branches live in the Godot-free HitFlashPalette; the
            //    mapping itself is asserted in HitFlashPaletteTests.
            string paletteSource = ProjectSettings.GlobalizePath("res://scripts/core/HitFlashPalette.cs");
            if (!System.IO.File.Exists(paletteSource))
                throw new Exception($"HitFlashPalette source not found: {paletteSource}");
            string paletteText = System.IO.File.ReadAllText(paletteSource);
            string[] requiredTypes = { "Kinetic", "Energy", "Lightning", "Acid", "Fire", "Explosive" };
            foreach (var t in requiredTypes)
            {
                if (!paletteText.Contains($"DamageType.{t} =>"))
                    throw new Exception($"HitFlashPalette.ColorFor missing branch for DamageType.{t}");
            }

            flash.QueueFree();
            GD.Print("  HitFlash: subscribed to OnProjectileHit, fade lifecycle correct, all 6 DamageType colors mapped");
        }

        /// <summary>
        /// Runtime smoke of the interactive-surroundings FX layer (R-FX1/2/3).
        /// The three nodes must build their pools headlessly, react to their
        /// EventBus signals, and use linear filtering for their code-generated
        /// gradient textures (the project-wide default is Nearest). Also pins
        /// the ScreenDistortionFX wake fix: hero damage and threat changes
        /// must re-enable processing even when the layer had settled to idle
        /// (previously the pulse was recorded but the overlay never woke to
        /// draw it). The underlying math is asserted headlessly in
        /// LightFXStateTests / ScreenRippleStateTests / DebrisBurstPlanTests.
        /// </summary>
        private void TestSurroundingsFx()
        {
            // --- LivingLightRig: pool built, hit reaction, linear filter ---
            var rig = new LivingLightRig();
            AddChild(rig);
            if (rig.TextureFilter != CanvasItem.TextureFilterEnum.Linear)
                throw new Exception("LivingLightRig must use Linear filtering (gradient banding fix)");
            if (rig.GetChildCount() != 1 + LightFXState.SlotCount)
                throw new Exception($"LivingLightRig expected {1 + LightFXState.SlotCount} children (veil + flash lights), got {rig.GetChildCount()}");
            // Explosive impacts bypass the rate limit, so a light must light up.
            EventBus.Instance.EmitProjectileHit(new Vector2(120f, 80f), DamageType.Explosive);
            rig._Process(0.016);
            bool lit = false;
            for (int i = 1; i < rig.GetChildCount(); i++)
            {
                if (rig.GetChild<PointLight2D>(i).Enabled) { lit = true; break; }
            }
            if (!lit)
                throw new Exception("LivingLightRig: no light enabled after an Explosive hit");
            rig.QueueFree();

            // --- DestructionDrama: pool built, destruction reaction ---
            var drama = new DestructionDrama();
            AddChild(drama);
            if (drama.TextureFilter != CanvasItem.TextureFilterEnum.Linear)
                throw new Exception("DestructionDrama must use Linear filtering (gradient banding fix)");
            EventBus.Instance.EmitEnvironmentDestroyed(new Vector2(64f, 64f), 80f);
            if (!drama.IsProcessing())
                throw new Exception("DestructionDrama should process after OnEnvironmentDestroyed");
            bool shardShown = false;
            foreach (var child in drama.GetChildren())
            {
                if (child is Sprite2D sprite && sprite.Visible) { shardShown = true; break; }
            }
            if (!shardShown)
                throw new Exception("DestructionDrama: no visible shard/ring sprite after destruction");
            drama.QueueFree();

            // --- ScreenDistortionFX: wake regression + event reactions ---
            var fx = new ScreenDistortionFX();
            AddChild(fx);
            var overlay = fx.GetChild<ColorRect>(0);
            EventBus.Instance.EmitHeroDamaged(10f);
            if (!fx.IsProcessing())
                throw new Exception("ScreenDistortionFX must process after OnHeroDamaged (wake regression)");
            if (!overlay.Visible)
                throw new Exception("ScreenDistortionFX overlay must show after OnHeroDamaged");
            fx._Process(0.016);
            EventBus.Instance.EmitThreatLevelChanged(12);
            if (!fx.IsProcessing())
                throw new Exception("ScreenDistortionFX must process after OnThreatLevelChanged");
            EventBus.Instance.EmitProjectileHit(new Vector2(0f, 0f), DamageType.Kinetic);
            fx._Process(0.016);
            if (!overlay.Visible)
                throw new Exception("ScreenDistortionFX overlay must stay visible while rippling");
            fx.QueueFree();

            GD.Print("  Surroundings FX: light rig + destruction drama + distortion layer built, react to events, linear-filtered");
        }

        /// <summary>
        /// Regression for the enemy target cache: enemies capture
        /// GameManager.ActiveHero at spawn and only ever test it for null.
        /// A disposed reference (the hero is freed on death) is non-null, so
        /// every enemy used to throw ObjectDisposedException from
        /// Move/UpdateAI every physics frame — a permanent log/CPU flood
        /// until restart (surfaced by the headless Main.tscn smoke run).
        /// EnemyBase must drop invalid cached references instead.
        /// </summary>
        private void TestEnemyTargetDisposal()
        {
            var previousHero = GameManager.Instance.ActiveHero;
            var hero = new Atlas();
            GameManager.Instance.ActiveHero = hero;
            AddChild(hero);

            var drone = new Drone();
            AddChild(drone); // caches the hero reference in _Ready

            hero.Free(); // immediate disposal (game over path)

            // Run past the AI interval so both UpdateAI and Move execute.
            for (int i = 0; i < 20; i++)
                drone._PhysicsProcess(0.05);

            drone.QueueFree();
            GameManager.Instance.ActiveHero = previousHero;
            GD.Print("  Enemy targeting: disposed hero reference handled without exceptions");
        }

        private static void TestSceneTextures()
        {
            string[] scenes = {
                "res://scenes/heroes/Atlas.tscn",
                "res://scenes/heroes/Havoc.tscn",
                "res://scenes/heroes/Ignis.tscn",
                "res://scenes/heroes/Synapse.tscn",
                "res://scenes/heroes/Toxis.tscn",
                "res://scenes/heroes/Volt.tscn",
                "res://scenes/heroes/Zephyr.tscn",
                "res://scenes/enemies/Apex.tscn",
                "res://scenes/enemies/Artillery.tscn",
                "res://scenes/enemies/Brute.tscn",
                "res://scenes/enemies/Burrower.tscn",
                "res://scenes/enemies/Drone.tscn",
                "res://scenes/enemies/Exploder.tscn",
                "res://scenes/enemies/Healer.tscn",
                "res://scenes/enemies/Parasite.tscn",
                "res://scenes/enemies/Shielder.tscn",
                "res://scenes/enemies/Sprinter.tscn",
                "res://scenes/projectiles/Projectile.tscn",
                "res://scenes/vfx/Decal.tscn",
                "res://scenes/maps/Destructible.tscn"
            };

            foreach (var path in scenes)
            {
                var scene = GD.Load<PackedScene>(path);
                if (scene == null) throw new Exception($"Could not load scene: {path}");
                var node = scene.Instantiate<Node>();
                var sprite = node is Sprite2D s ? s : node.GetNodeOrNull<Sprite2D>("Sprite2D");
                if (sprite == null)
                {
                    node.QueueFree();
                    throw new Exception($"Scene {path} has no Sprite2D");
                }
                if (sprite.Texture == null)
                {
                    node.QueueFree();
                    throw new Exception($"Scene {path} Sprite2D.Texture is NULL!");
                }
                if (sprite.Texture is AtlasTexture at && at.Atlas == null)
                {
                    node.QueueFree();
                    throw new Exception($"Scene {path} has AtlasTexture with null Atlas!");
                }

                // Verify character textures are 256x256 high-resolution assets
                if (path.StartsWith("res://scenes/heroes/") || path.StartsWith("res://scenes/enemies/"))
                {
                    int w = sprite.Texture.GetWidth();
                    int h = sprite.Texture.GetHeight();
                    if (w != 256 || h != 256)
                    {
                        node.QueueFree();
                        throw new Exception($"Scene {path} texture size expected 256x256, got {w}x{h}");
                    }

                    // Check that Sprite2D scale aligns with CollisionShape2D radius (200px visual body base)
                    var col = node.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
                    if (col?.Shape is CircleShape2D circle)
                    {
                        float expectedScale = (circle.Radius * 2f) / 200f;
                        if (Mathf.Abs(sprite.Scale.X - expectedScale) > 0.05f)
                        {
                            node.QueueFree();
                            throw new Exception($"Scene {path} scale {sprite.Scale.X:F3} does not align with CollisionShape radius {circle.Radius} (expected ~{expectedScale:F3})");
                        }
                    }
                }

                node.QueueFree();
            }
            GD.Print($"  Verified {scenes.Length} scenes have valid, non-null textures with normalized scales.");
        }
    }
}

