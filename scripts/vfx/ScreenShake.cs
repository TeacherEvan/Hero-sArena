using Godot;

namespace HeroArena
{
    /// <summary>
    /// Noise-based camera shake. Attach to Camera2D.
    /// Call Shake(intensity, duration) to trigger.
    /// </summary>
    public partial class ScreenShake : Camera2D
    {
        private float _intensity = 0f;
        private float _duration = 0f;
        private float _elapsed = 0f;
        private readonly FastNoiseLite _noise = new();

        public override void _Ready()
        {
            _noise.Seed = (int)GD.Randi();
            _noise.Frequency = 4f;
        }

        public void Shake(float intensity, float duration)
        {
            _intensity = intensity;
            _duration = duration;
            _elapsed = 0f;
        }

        public override void _Process(double delta)
        {
            if (_elapsed >= _duration)
            {
                Offset = Vector2.Zero;
                return;
            }

            _elapsed += (float)delta;
            float t = 1f - (_elapsed / _duration);
            float nx = _noise.GetNoise2D(_elapsed * 100f, 0f);
            float ny = _noise.GetNoise2D(0f, _elapsed * 100f);
            Offset = new Vector2(nx, ny) * _intensity * t;
        }
    }
}
