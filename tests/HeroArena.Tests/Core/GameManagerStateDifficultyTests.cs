using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Core;

// R2 (Difficulty Selector): pure difficulty settings extracted to
// GameManagerState so the entire difficulty surface runs headlessly.
// GameManager delegates 1:1; its remaining Node wiring (EventBus, SceneTree)
// stays guarded by the Godot gate.
public class GameManagerStateDifficultyTests
{
    private GameManagerState NewState() => new();

    // ── Default ─────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultDifficulty_IsNormal()
    {
        var state = NewState();
        state.CurrentDifficulty.Should().Be(Difficulty.Normal);
    }

    // ── SetDifficulty ───────────────────────────────────────────────────────

    [Fact]
    public void SetDifficulty_ChangesCurrentDifficulty()
    {
        var state = NewState();
        state.SetDifficulty(Difficulty.Easy);
        state.CurrentDifficulty.Should().Be(Difficulty.Easy);
        state.SetDifficulty(Difficulty.Hard);
        state.CurrentDifficulty.Should().Be(Difficulty.Hard);
    }

    [Fact]
    public void SetDifficulty_SameValue_IsNoOp()
    {
        var state = NewState();
        state.SetDifficulty(Difficulty.Normal);
        state.CurrentDifficulty.Should().Be(Difficulty.Normal); // unchanged, no throw
    }

    // ── GetSettings Easy ────────────────────────────────────────────────────

    [Fact]
    public void GetSettings_Easy_SpawnsFewerEnemies_LowerThreat_MoreBreather()
    {
                var s = GameManagerState.GetSettings(Difficulty.Easy);

        s.EnemyCountMultiplier.Should().Be(0.6f);
        s.EnemyHealthMultiplier.Should().Be(0.7f);
        s.EnemyDamageMultiplier.Should().Be(0.7f);
        s.ThreatInterval.Should().Be(20); // threat steps slower
        s.MutatorScale.Should().Be(0.5f);
        s.XpMultiplier.Should().Be(1.5f);
        s.BreatherSeconds.Should().Be(8f);
    }

    // ── GetSettings Normal ──────────────────────────────────────────────────

    [Fact]
    public void GetSettings_Normal_BaseValues()
    {
                var s = GameManagerState.GetSettings(Difficulty.Normal);

        s.EnemyCountMultiplier.Should().Be(1.0f);
        s.EnemyHealthMultiplier.Should().Be(1.0f);
        s.EnemyDamageMultiplier.Should().Be(1.0f);
        s.ThreatInterval.Should().Be(10);
        s.MutatorScale.Should().Be(1.0f);
        s.XpMultiplier.Should().Be(1.0f);
        s.BreatherSeconds.Should().Be(5f);
    }

    // ── GetSettings Hard ────────────────────────────────────────────────────

    [Fact]
    public void GetSettings_Hard_SpawnsMoreEnemies_FasterThreat_LessBreather()
    {
                var s = GameManagerState.GetSettings(Difficulty.Hard);

        s.EnemyCountMultiplier.Should().Be(1.4f);
        s.EnemyHealthMultiplier.Should().Be(1.3f);
        s.EnemyDamageMultiplier.Should().Be(1.3f);
        s.ThreatInterval.Should().Be(8); // threat steps faster
        s.MutatorScale.Should().Be(1.5f);
        s.XpMultiplier.Should().Be(0.8f);
        s.BreatherSeconds.Should().Be(4f);
    }

    // ── RecordEnvironmentDestroyed respects difficulty ThreatInterval ───────

    [Fact]
    public void RecordEnvironmentDestroyed_Easy_ThreatInterval20()
    {
        var state = NewState();
        state.SetDifficulty(Difficulty.Easy);

        // 19 destructions → threat 0 (19/20 = 0)
        for (int i = 0; i < 19; i++) state.RecordEnvironmentDestroyed();
        state.ThreatLevel.Should().Be(0);

        // 20th destruction → threat 1
        state.RecordEnvironmentDestroyed();
        state.ThreatLevel.Should().Be(1);
    }

    [Fact]
    public void RecordEnvironmentDestroyed_Normal_ThreatInterval10()
    {
        var state = NewState(); // defaults to Normal

        for (int i = 0; i < 9; i++) state.RecordEnvironmentDestroyed();
        state.ThreatLevel.Should().Be(0);

        state.RecordEnvironmentDestroyed();
        state.ThreatLevel.Should().Be(1);
    }

    [Fact]
    public void RecordEnvironmentDestroyed_Hard_ThreatInterval8()
    {
        var state = NewState();
        state.SetDifficulty(Difficulty.Hard);

        for (int i = 0; i < 7; i++) state.RecordEnvironmentDestroyed();
        state.ThreatLevel.Should().Be(0);

        state.RecordEnvironmentDestroyed();
        state.ThreatLevel.Should().Be(1);
    }

    [Fact]
    public void RecordEnvironmentDestroyed_NeverStepsBackward()
    {
        var state = NewState();
        state.SetDifficulty(Difficulty.Hard);

        // Build threat to 2 (16 destructions / 8 = 2)
        for (int i = 0; i < 16; i++) state.RecordEnvironmentDestroyed();
        state.ThreatLevel.Should().Be(2);

        // Switch to Easy (interval 20) — existing threat must not drop
        state.SetDifficulty(Difficulty.Easy);
        state.ThreatLevel.Should().Be(2);

        // 3 more destructions = 19 total; 19/20 = 0, but threat stays at 2
        state.RecordEnvironmentDestroyed();
        state.RecordEnvironmentDestroyed();
        state.RecordEnvironmentDestroyed();
        state.ThreatLevel.Should().Be(2);
    }
}
