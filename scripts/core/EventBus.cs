using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Singleton event bus for decoupled communication between game systems.
    /// Register as Autoload in project settings.
    /// </summary>
    public partial class EventBus : Node
    {
        public static EventBus Instance { get; private set; } = null!;

        // ── Enemy / Wave ──────────────────────────────────────────────────────
        public event Action<EnemyBase>? OnEnemyKilled;
        public event Action<int>? OnWaveStarted;
        public event Action<int>? OnWaveCompleted;
        public event Action<int>? OnThreatLevelChanged;

        // ── Hero ──────────────────────────────────────────────────────────────
        public event Action<float>? OnHeroDamaged;
        public event Action<int>? OnLevelUp;
        public event Action<string>? OnPowerupCollected;

        // ── Environment ───────────────────────────────────────────────────────
        public event Action<Vector2, float>? OnEnvironmentDestroyed;

        // ── Projectile / VFX ─────────────────────────────────────────────────
        public event Action<Vector2, DamageType>? OnProjectileHit;
        public event Action<Vector2, DecalType, float>? OnDecalRequested;

        public override void _Ready()
        {
            Instance = this;
        }

        // ── Emitters ─────────────────────────────────────────────────────────
        public void EmitEnemyKilled(EnemyBase enemy) => OnEnemyKilled?.Invoke(enemy);
        public void EmitHeroDamaged(float damage) => OnHeroDamaged?.Invoke(damage);
        public void EmitWaveStarted(int wave) => OnWaveStarted?.Invoke(wave);
        public void EmitWaveCompleted(int wave) => OnWaveCompleted?.Invoke(wave);
        public void EmitLevelUp(int level) => OnLevelUp?.Invoke(level);
        public void EmitPowerupCollected(string type) => OnPowerupCollected?.Invoke(type);
        public void EmitEnvironmentDestroyed(Vector2 pos, float radius) => OnEnvironmentDestroyed?.Invoke(pos, radius);
        public void EmitThreatLevelChanged(int level) => OnThreatLevelChanged?.Invoke(level);
        public void EmitProjectileHit(Vector2 pos, DamageType type) => OnProjectileHit?.Invoke(pos, type);
        public void EmitDecalRequested(Vector2 pos, DecalType type, float size) => OnDecalRequested?.Invoke(pos, type, size);
    }
}
