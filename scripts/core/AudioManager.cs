using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    public enum SoundCue
    {
        ShootLaser,
        ShootFire,
        ShootAcid,
        Explosion,
        HitImpact,
        Dodge,
        Ability,
        LevelUp
    }

    /// <summary>
    /// Lightweight procedural audio synthesizer for Hero's Arena.
    /// Generates clean 16-bit retro arcade PCM sounds in memory with zero external asset files.
    /// Autoload or dynamically instantiated node with pooled players.
    /// </summary>
    public partial class AudioManager : Node
    {
        public static AudioManager? Instance { get; private set; }

        private const int MIX_RATE = 22050;
        private const int POOL_SIZE = 12;

        private readonly Dictionary<SoundCue, AudioStreamWav> _soundCache = new();
        private readonly List<AudioStreamPlayer> _players = new();
        private int _playerIndex = 0;

        // Delegate fields so subscriptions can be removed deterministically in _ExitTree.
        // Storing the delegates avoids leaking the AudioManager instance (and its
        // 12 AudioStreamPlayer children) into the singleton EventBus forever.
        private Action<int>? _onLevelUp;
        private Action<Vector2, DamageType>? _onProjectileHit;

        public override void _Ready()
        {
            Instance = this;
            PrebakeSounds();

            for (int i = 0; i < POOL_SIZE; i++)
            {
                var player = new AudioStreamPlayer
                {
                    Bus = "Master",
                    VolumeDb = -4.0f
                };
                AddChild(player);
                _players.Add(player);
            }

            if (EventBus.Instance != null)
            {
                _onLevelUp = _ => PlaySound(SoundCue.LevelUp, -2f);
                _onProjectileHit = (_, type) =>
                {
                    if (type == DamageType.Explosive) PlaySound(SoundCue.Explosion, -6f);
                    else if (type == DamageType.Acid) PlaySound(SoundCue.ShootAcid, -10f);
                    else PlaySound(SoundCue.HitImpact, -12f);
                };
                EventBus.Instance.OnLevelUp += _onLevelUp;
                EventBus.Instance.OnProjectileHit += _onProjectileHit;
            }
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnLevelUp -= _onLevelUp;
                EventBus.Instance.OnProjectileHit -= _onProjectileHit;
            }
            if (Instance == this) Instance = null;
            base._ExitTree();
        }

        public void PlaySound(SoundCue cue, float volumeDb = -4.0f)
        {
            if (_players.Count == 0) return;
            if (!_soundCache.TryGetValue(cue, out var stream)) return;

            var player = _players[_playerIndex];
            _playerIndex = (_playerIndex + 1) % _players.Count;

            player.Stream = stream;
            player.VolumeDb = volumeDb;
            player.Play();
        }

        private static readonly float[] AbilityFrequencies = new[] { 330f, 440f, 550f };
        private static readonly float[] LevelUpNotes = new[] { 440f, 554f, 659f, 880f };

        private void PrebakeSounds()
        {
            _soundCache[SoundCue.ShootLaser] = CreateWav(GenerateTone(880f, 220f, 0.08f, 0.4f));
            _soundCache[SoundCue.ShootFire] = CreateWav(GenerateNoise(0.12f, 0.45f, true));
            _soundCache[SoundCue.ShootAcid] = CreateWav(GenerateChirp(340f, 160f, 0.12f, 0.5f));
            _soundCache[SoundCue.Explosion] = CreateWav(GenerateExplosion(0.28f, 0.7f));
            _soundCache[SoundCue.HitImpact] = CreateWav(GenerateTone(150f, 60f, 0.04f, 0.35f));
            _soundCache[SoundCue.Dodge] = CreateWav(GenerateTone(200f, 600f, 0.11f, 0.35f));
            _soundCache[SoundCue.Ability] = CreateWav(GenerateChord(AbilityFrequencies, 0.28f, 0.5f));
            _soundCache[SoundCue.LevelUp] = CreateWav(GenerateArpeggio(LevelUpNotes, 0.35f, 0.55f));
        }

        private static AudioStreamWav CreateWav(byte[] pcmData)
        {
            var wav = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = MIX_RATE,
                Stereo = false,
                Data = pcmData
            };
            return wav;
        }

        private static byte[] GenerateTone(float startFreq, float endFreq, float duration, float maxGain)
        {
            int totalSamples = (int)(duration * MIX_RATE);
            byte[] data = new byte[totalSamples * 2];
            double phase = 0;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t);
                phase += 2.0 * Math.PI * currentFreq / MIX_RATE;

                float envelope = (1.0f - t);
                short sample = (short)(Math.Sin(phase) * envelope * maxGain * short.MaxValue);

                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return data;
        }

        private static byte[] GenerateChirp(float startFreq, float endFreq, float duration, float maxGain)
        {
            int totalSamples = (int)(duration * MIX_RATE);
            byte[] data = new byte[totalSamples * 2];
            double phase = 0;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float mod = (float)Math.Sin(t * 30.0) * 40f;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t) + mod;
                phase += 2.0 * Math.PI * currentFreq / MIX_RATE;

                float envelope = Math.Min(t * 10f, 1f) * (1.0f - t);
                short sample = (short)(Math.Sin(phase) * envelope * maxGain * short.MaxValue);

                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return data;
        }

        private static byte[] GenerateNoise(float duration, float maxGain, bool lowpass)
        {
            int totalSamples = (int)(duration * MIX_RATE);
            byte[] data = new byte[totalSamples * 2];
            var rng = new Random(42);
            float last = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float white = ((float)rng.NextDouble() * 2f) - 1f;
                if (lowpass)
                {
                    last = Mathf.Lerp(last, white, 0.25f);
                    white = last;
                }

                float envelope = Math.Min(t * 8f, 1f) * (1.0f - t);
                short sample = (short)(white * envelope * maxGain * short.MaxValue);

                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return data;
        }

        private static byte[] GenerateExplosion(float duration, float maxGain)
        {
            int totalSamples = (int)(duration * MIX_RATE);
            byte[] data = new byte[totalSamples * 2];
            var rng = new Random(77);
            float lowFilter = 0f;
            double bassPhase = 0;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float white = ((float)rng.NextDouble() * 2f) - 1f;
                lowFilter = Mathf.Lerp(lowFilter, white, 0.18f);

                bassPhase += 2.0 * Math.PI * Mathf.Lerp(80f, 30f, t) / MIX_RATE;
                float bass = (float)Math.Sin(bassPhase);

                float mixed = (lowFilter * 0.7f) + (bass * 0.5f);
                float envelope = Math.Min(t * 20f, 1f) * (float)Math.Pow(1.0f - t, 1.8);
                short sample = (short)(Math.Clamp(mixed * envelope * maxGain, -1f, 1f) * short.MaxValue);

                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return data;
        }

        private static byte[] GenerateChord(float[] frequencies, float duration, float maxGain)
        {
            int totalSamples = (int)(duration * MIX_RATE);
            byte[] data = new byte[totalSamples * 2];
            double[] phases = new double[frequencies.Length];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float mixed = 0f;

                for (int f = 0; f < frequencies.Length; f++)
                {
                    phases[f] += 2.0 * Math.PI * frequencies[f] / MIX_RATE;
                    mixed += (float)Math.Sin(phases[f]);
                }
                mixed /= frequencies.Length;

                float envelope = (1.0f - t);
                short sample = (short)(mixed * envelope * maxGain * short.MaxValue);

                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return data;
        }

        private static byte[] GenerateArpeggio(float[] notes, float duration, float maxGain)
        {
            int totalSamples = (int)(duration * MIX_RATE);
            byte[] data = new byte[totalSamples * 2];
            int noteCount = notes.Length;
            int samplesPerNote = totalSamples / noteCount;
            double phase = 0;

            for (int i = 0; i < totalSamples; i++)
            {
                int noteIndex = Math.Min(i / samplesPerNote, noteCount - 1);
                float freq = notes[noteIndex];
                phase += 2.0 * Math.PI * freq / MIX_RATE;

                float noteProgress = (float)(i % samplesPerNote) / samplesPerNote;
                float envelope = 1.0f - (noteProgress * 0.4f);
                short sample = (short)(Math.Sin(phase) * envelope * maxGain * short.MaxValue);

                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return data;
        }
    }
}
