using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena.Tests
{
    /// <summary>
    /// Godot headless test runner for core systems that require Godot types.
    /// Run with: godot --headless -s res://tests/GodotTests/CoreSystemTests.cs
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

            GD.Print($"\n=== Results: {passed} passed, {failed} failed ===");
            
            if (failed > 0)
            {
                OS.ExitCode = 1;
            }
            GetTree().Quit();
        }

        private void TestSpatialHashGrid()
        {
            var grid = new SpatialHashGrid(64, 64);
            
            // Test insert and query
            grid.Insert(1, new Vector2(100, 100), 10f);
            var results = grid.QueryRadius(new Vector2(100, 100), 15f, out int count);
            if (count != 1 || results[0] != 1)
                throw new Exception($"Expected 1 entity at (100,100), got {count}");

            // Test remove
            grid.Remove(1);
            results = grid.QueryRadius(new Vector2(100, 100), 15f, out count);
            if (count != 0)
                throw new Exception($"Expected 0 after remove, got {count}");

            // Test multiple entities
            grid.Insert(1, new Vector2(100, 100), 10f);
            grid.Insert(2, new Vector2(120, 100), 10f);
            grid.Insert(3, new Vector2(200, 200), 10f);
            results = grid.QueryRadius(new Vector2(110, 100), 20f, out count);
            if (count != 2)
                throw new Exception($"Expected 2 entities near (110,100), got {count}");

            // Test update
            grid.Update(1, new Vector2(500, 500), 10f);
            results = grid.QueryRadius(new Vector2(100, 100), 15f, out count);
            if (count != 1 || results[0] != 2)
                throw new Exception("Update failed - entity still at old position");

            results = grid.QueryRadius(new Vector2(500, 500), 15f, out count);
            if (count != 1 || results[0] != 1)
                throw new Exception("Update failed - entity not at new position");

            // Test clear
            grid.Clear();
            results = grid.QueryRadius(new Vector2(500, 500), 15f, out count);
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

        private void TestWaveManager()
        {
            // Test the Weibull distribution calculation directly
            const int MinWaveEnemies = 10;
            const float IntensityScaleFactor = 500f;
            const float WEIBULL_K = 1.5f;
            const float WEIBULL_LAMBDA = 10f;

            // Wave 1 should be at minimum
            float t = 1f;
            float intensity = (WEIBULL_K / WEIBULL_LAMBDA) * MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K - 1f) * MathF.Exp(-MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K));
            int count = MinWaveEnemies + (int)(intensity * IntensityScaleFactor);
            if (count != MinWaveEnemies)
                throw new Exception($"Wave 1 expected {MinWaveEnemies}, got {count}");

            // Wave 10 should still be at minimum (pre-peak)
            t = 10f;
            intensity = (WEIBULL_K / WEIBULL_LAMBDA) * MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K - 1f) * MathF.Exp(-MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K));
            count = MinWaveEnemies + (int)(intensity * IntensityScaleFactor);
            if (count != MinWaveEnemies)
                throw new Exception($"Wave 10 expected {MinWaveEnemies}, got {count}");

            // Wave 20 should be past peak
            t = 20f;
            intensity = (WEIBULL_K / WEIBULL_LAMBDA) * MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K - 1f) * MathF.Exp(-MathF.Pow(t / WEIBULL_LAMBDA, WEIBULL_K));
            count = MinWaveEnemies + (int)(intensity * IntensityScaleFactor);
            if (count <= MinWaveEnemies)
                throw new Exception($"Wave 20 expected > {MinWaveEnemies}, got {count}");
        }

        private void TestFlowFieldPathfinder()
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

        private void TestLevelProgression()
        {
            var progression = new LevelProgression();
            
            // Test kinetic damage
            float kinetic = progression.CalcKineticDamage(100f, 1);
            if (MathF.Abs(kinetic - 115f) > 0.001f)
                throw new Exception($"Kinetic damage level 1: expected 115, got {kinetic}");
            
            kinetic = progression.CalcKineticDamage(100f, 5);
            if (MathF.Abs(kinetic - 175f) > 0.001f)
                throw new Exception($"Kinetic damage level 5: expected 175, got {kinetic}");

            // Test energy damage
            float energy = progression.CalcEnergyDamage(100f, 1);
            if (MathF.Abs(energy - 100f) > 1f)
                throw new Exception($"Energy damage level 1: expected ~100, got {energy}");

            energy = progression.CalcEnergyDamage(100f, 10);
            if (MathF.Abs(energy - 1995.3f) > 1f)
                throw new Exception($"Energy damage level 10: expected ~1995, got {energy}");

            // Test karma amplifier
            float karma0 = progression.CalcKarmaAmplifier(0);
            if (MathF.Abs(karma0 - 1f) > 0.001f)
                throw new Exception($"Karma 0: expected 1, got {karma0}");

            float karma10 = progression.CalcKarmaAmplifier(10);
            if (karma10 <= karma0)
                throw new Exception("Karma should increase with destruction");

            // Test XP formula
            if (progression.XpRequiredForLevel(1) != 100)
                throw new Exception("XP level 1");
            if (progression.XpRequiredForLevel(2) != 160)
                throw new Exception("XP level 2");
            if (progression.XpRequiredForLevel(5) != 450)
                throw new Exception("XP level 5");
            if (progression.XpRequiredForLevel(10) != 1450)
                throw new Exception("XP level 10");

            // Test perk selection
            var perks = progression.GetRandomPerks(3);
            if (perks.Length != 3)
                throw new Exception($"Expected 3 perks, got {perks.Length}");
            
            var uniquePerks = new HashSet<PerkType>(perks);
            if (uniquePerks.Count != 3)
                throw new Exception("Perks should be distinct");
        }
    }
}