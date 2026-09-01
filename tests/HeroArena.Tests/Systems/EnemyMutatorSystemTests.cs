using System;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;
using FluentAssertions;
using HeroArena;

namespace HeroArena.Tests.Systems;

public partial class DummyEnemy : EnemyBase
{
    protected override void UpdateAI() {}

    // Override methods that trigger Godot methods or signals
    protected override void Die() {}
    public override void TakeDamage(float amount, DamageType type = DamageType.Kinetic) {}
}

public class EnemyMutatorSystemTests
{
    private DummyEnemy CreateDummyEnemy()
    {
#pragma warning disable SYSLIB0050
        return (DummyEnemy)FormatterServices.GetUninitializedObject(typeof(DummyEnemy));
#pragma warning restore SYSLIB0050
    }

    [Fact]
    public void ApplySingleMutator_SpeedBoost_IncreasesMoveSpeed()
    {
        var enemy = CreateDummyEnemy();
        enemy.MoveSpeed = 100f;

        var method = typeof(EnemyMutatorSystem).GetMethod("ApplySingleMutator", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { enemy, MutatorType.SpeedBoost, 2 });

        enemy.MoveSpeed.Should().BeApproximately(120f, 0.001f);
    }

    [Fact]
    public void ApplySingleMutator_HealthBoost_IncreasesMaxAndCurrentHealth()
    {
        var enemy = CreateDummyEnemy();
        enemy.MaxHealth = 100f;
        typeof(EnemyBase).GetProperty("CurrentHealth", BindingFlags.Public | BindingFlags.Instance)!.SetValue(enemy, 100f);

        var method = typeof(EnemyMutatorSystem).GetMethod("ApplySingleMutator", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { enemy, MutatorType.HealthBoost, 5 });

        // scale = 1.0f + 0.1f * 5 = 1.5f
        enemy.MaxHealth.Should().BeApproximately(150f, 0.001f);
        enemy.CurrentHealth.Should().BeApproximately(150f, 0.001f);
    }

    [Fact]
    public void ApplySingleMutator_DamageBoost_IncreasesDamage()
    {
        var enemy = CreateDummyEnemy();
        enemy.Damage = 10f;

        var method = typeof(EnemyMutatorSystem).GetMethod("ApplySingleMutator", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { enemy, MutatorType.DamageBoost, 3 });

        // scale = 1.0f + 0.1f * 3 = 1.3f
        enemy.Damage.Should().BeApproximately(13f, 0.001f);
    }

    [Fact]
    public void ApplySingleMutator_Shielded_AddsShieldCharges()
    {
        var enemy = CreateDummyEnemy();

        var method = typeof(EnemyMutatorSystem).GetMethod("ApplySingleMutator", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { enemy, MutatorType.Shielded, 5 });

        // charges = Mathf.Max(1, 5 / 2) = Mathf.Max(1, 2) = 2
        enemy.ShieldCharges.Should().Be(2);
    }

    [Fact]
    public void ApplySingleMutator_Enraged_IncreasesSpeedAndDamage()
    {
        var enemy = CreateDummyEnemy();
        enemy.MoveSpeed = 100f;
        enemy.Damage = 10f;

        var method = typeof(EnemyMutatorSystem).GetMethod("ApplySingleMutator", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { enemy, MutatorType.Enraged, 5 });

        enemy.MoveSpeed.Should().BeApproximately(150f, 0.001f);
        enemy.Damage.Should().BeApproximately(15f, 0.001f);
    }
}
