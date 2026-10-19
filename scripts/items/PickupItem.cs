using Godot;

namespace HeroArena
{
    /// <summary>
    /// A weapon or power-up lying on the ground, Crimsonland-style: the hero
    /// walks over it to pick it up. Weapons auto-equip (one weapon at a time,
    /// re-equipping tops off ammo); ammo pickups refill the current weapon.
    ///
    /// Pooled via <see cref="ObjectPoolManager"/> would be ideal for high
    /// drop rates; this version instantiates on drop and queues itself free
    /// on pickup so it is simple and safe.
    /// </summary>
    public partial class PickupItem : Area2D
    {
        [Export] public WeaponId Weapon { get; set; } = WeaponId.None;
        [Export] public bool IsAmmo { get; set; }
        [Export] public int AmmoAmount { get; set; } = 0;
        [Export] public bool IsBomb { get; set; }   // screen-clear nuke (Crimsonland)
        [Export] public float AutoPickupSeconds = 0f; // 0 = touch only

        private CollisionShape2D? _shape;
        private float _autoTimer;

        public override void _Ready()
        {
            _shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            BodyEntered += OnBodyEntered;

            // Fade in briefly so the drop is legible against the action.
            Modulate = new Color(1f, 1f, 1f, 0f);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // Fade in.
            var mod = Modulate;
            if (mod.A < 1f)
            {
                mod.A = Mathf.Min(1f, mod.A + dt * 3f);
                Modulate = mod;
            }

            // Optional auto-pickup after a grace period (Crimsonland power-ups
            // can be grabbed by walking over; Telekinesis perk activates at
            // range — not modeled here).
            if (AutoPickupSeconds > 0f)
            {
                _autoTimer += dt;
                if (_autoTimer >= AutoPickupSeconds)
                {
                    QueueFree();
                }
            }
        }

        private void OnBodyEntered(Node body)
        {
            if (body is not HeroBase hero) return;
            if (IsBomb)
            {
                TriggerBomb(hero);
            }
            else if (Weapon != WeaponId.None)
            {
                hero.EquipWeapon(Weapon);
            }
            else if (IsAmmo)
            {
                hero.AddAmmo(AmmoAmount);
            }

            // Only the first hero to touch it gets it.
            QueueFree();
        }

        /// <summary>Crimsonland Nuke: screen-clear blast that damages every
        /// living enemy (scaled so it remains dangerous late-game).</summary>
        private static void TriggerBomb(HeroBase hero)
        {
            var grid = GameManager.Instance?.SpatialGrid;
            if (grid == null) return;

            // Query a very large radius centered on the hero to catch every
            // enemy on screen.
            int[] hits = grid.QueryRadius(hero.GlobalPosition, 4000f, out int count, out bool _);
            float dmg = hero.Stats.DamageMult * 9999f; // lethal to any enemy
            for (int i = 0; i < count; i++)
            {
                if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                    && enemy.State != EnemyAIState.Dead)
                {
                    enemy.TakeDamage(dmg, DamageType.Explosive);
                }
            }
            EventBus.Instance?.EmitDecalRequested(hero.GlobalPosition, DecalType.ExplosionScorch, 300f);
            EventBus.Instance?.EmitPowerupCollected("Bomb");
        }
    }
}
