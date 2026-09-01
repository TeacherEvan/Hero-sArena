using Godot;

namespace HeroArena
{
    public struct ProjectileData
    {
        public Vector2 Position;
        public Vector2 Direction;
        public float Speed;
        public float Damage;
        public DamageType Type;

        public ProjectileData(Vector2 position, Vector2 direction, float speed, float damage, DamageType type)
        {
            Position = position;
            Direction = direction;
            Speed = speed;
            Damage = damage;
            Type = type;
        }
    }
}
