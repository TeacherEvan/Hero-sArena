using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// A single pooled decal instance (Sprite2D).
    /// Fades out over its configured lifetime then returns to pool.
    /// </summary>
    public partial class DecalInstance : Sprite2D
    {
        public int PoolIndex { get; private set; }
        public bool IsActive { get; private set; } = false;

        private float _lifetime = 0f;
        private float _maxLifetime = 10f;
        private float _fadeStart = 7f;

        private const float DefaultMaxLifetime = 10f;
        private const float DefaultFadeStart = 7f;

        public void Activate(Vector2 pos, DecalType type, float size, int poolIndex)
        {
            GlobalPosition = pos;
            PoolIndex = poolIndex;
            IsActive = true;
            _lifetime = 0f;
            Scale = Vector2.One * (size / 64f); // 64px base tile assumption

            (_maxLifetime, _fadeStart) = type switch
            {
                DecalType.AcidPool => (20f, 14f),
                DecalType.ExplosionScorch => (15f, 10f),
                DecalType.CraterMark => (30f, 25f),
                _ => (DefaultMaxLifetime, DefaultFadeStart)
            };

            Modulate = Colors.White;
            Visible = true;
            SetProcess(true);
        }

        public void Deactivate()
        {
            IsActive = false;
            Visible = false;
            SetProcess(false);
        }

        public override void _Process(double delta)
        {
            _lifetime += (float)delta;
            if (_lifetime >= _maxLifetime)
            {
                GameManager.Instance.PoolManager?.ReturnDecal(this);
                return;
            }
            if (_lifetime >= _fadeStart)
            {
                float t = (_lifetime - _fadeStart) / (_maxLifetime - _fadeStart);
                Modulate = new Color(1f, 1f, 1f, 1f - t);
            }
        }
    }
}
