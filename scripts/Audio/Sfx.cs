using System;
using System.Collections.Generic;
using Godot;
using Veld.Core;

namespace Veld.Audio;

/// <summary>
/// Procedurally generated placeholder sounds (no audio files needed): gunshots, clicks, footsteps,
/// hoofbeats, a whistle and coins. Each is an AudioStreamWav built once at startup and played through
/// a pooled AudioStreamPlayer3D. To use real recordings, drop .wav/.ogg files in res://audio and
/// replace the Build* calls with GD.Load&lt;AudioStream&gt;.
/// </summary>
public partial class Sfx : Node
{
    private const int Rate = 22050;
    private readonly Dictionary<string, AudioStreamWav> _streams = new();
    private readonly List<AudioStreamPlayer3D> _pool = new();
    private readonly Random _rnd = new(7);
    private AudioStreamPlayer _rainPlayer = null!;
    private AudioStreamPlayer _thunderPlayer = null!;
    private float _lp;

    public override void _Ready()
    {
        _streams["rifle"] = Build(0.55f, t => Noise() * Env(t, 0.004f, 0.18f) * 1.0f + Mathf.Sin(t * 90f * Mathf.Tau) * Env(t, 0.002f, 0.35f) * 0.5f);
        _streams["revolver"] = Build(0.35f, t => Noise() * Env(t, 0.003f, 0.10f) * 0.9f + Mathf.Sin(t * 140f * Mathf.Tau) * Env(t, 0.002f, 0.2f) * 0.4f);
        _streams["click"] = Build(0.08f, t => Noise() * Env(t, 0.001f, 0.015f) * 0.5f + Mathf.Sin(t * 1800f * Mathf.Tau) * Env(t, 0.001f, 0.03f) * 0.3f);
        _streams["hit"] = Build(0.2f, t => Noise() * Env(t, 0.002f, 0.06f) * 0.6f + Mathf.Sin(t * 60f * Mathf.Tau) * Env(t, 0.002f, 0.15f) * 0.5f);
        _streams["step"] = Build(0.12f, t => Noise() * Env(t, 0.002f, 0.04f) * 0.25f);
        _streams["hoof"] = Build(0.16f, t => Noise() * Env(t, 0.002f, 0.03f) * 0.35f + Mathf.Sin(t * 110f * Mathf.Tau) * Env(t, 0.002f, 0.07f) * 0.4f);
        _streams["whistle"] = Build(0.7f, t =>
        {
            float f = t < 0.3f ? 1500f + t * 2200f : 2160f - (t - 0.3f) * 900f;
            return Mathf.Sin(t * f * Mathf.Tau) * Env(t, 0.03f, 0.9f) * 0.35f;
        });
        _streams["engine"] = Build(1.0f, t => (Mathf.Sin(t * 70f * Mathf.Tau) * 0.5f + Mathf.Sin(t * 140f * Mathf.Tau) * 0.28f + Mathf.Sin(t * 210f * Mathf.Tau) * 0.14f) * (0.85f + Mathf.Sin(t * 35f * Mathf.Tau) * 0.15f) * 0.45f + Noise() * 0.05f);
        _streams["engine"].LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        _streams["engine"].LoopEnd = Rate;
        _streams["horn"] = Build(0.55f, t => (Mathf.Sin(t * 440f * Mathf.Tau) + Mathf.Sin(t * 554f * Mathf.Tau) * 0.8f) * Env(t, 0.02f, 0.9f) * 0.3f);
        _streams["coins"] = Build(0.4f, t =>
        {
            float s = 0f;
            for (int i = 0; i < 4; i++)
            {
                float start = i * 0.07f;
                if (t > start) s += Mathf.Sin((t - start) * (3200f + i * 600f) * Mathf.Tau) * Env(t - start, 0.001f, 0.12f) * 0.25f;
            }
            return s;
        });

        // rain: softened noise, looped; thunder: a slow low rumble
        var rain = Build(2.5f, t => { _lp += (Noise() - _lp) * 0.22f; return _lp * 1.3f; });
        rain.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        rain.LoopEnd = (int)(2.5f * Rate);
        _rainPlayer = new AudioStreamPlayer { Stream = rain, VolumeDb = -80f };
        AddChild(_rainPlayer);
        _lp = 0f;
        var thunder = Build(4f, t => { _lp += (Noise() - _lp) * 0.04f; float crack = t < 0.25f ? Noise() * Env(t, 0.01f, 0.08f) * 0.5f : 0f; return _lp * 5f * Env(t, 0.1f, 1.6f) + crack; });
        _thunderPlayer = new AudioStreamPlayer { Stream = thunder, VolumeDb = 2f };
        AddChild(_thunderPlayer);

        for (int i = 0; i < 12; i++)
        {
            var p = new AudioStreamPlayer3D { MaxDistance = 260f, UnitSize = 12f, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance, PanningStrength = 1.2f };
            AddChild(p);
            _pool.Add(p);
        }
        ApplyVolume(Game.Instance.Volume);
    }

    public static void ApplyVolume(float linear) => AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Mathf.Clamp(linear, 0.0001f, 1f)));

    private float Noise() => (float)(_rnd.NextDouble() * 2.0 - 1.0);
    private static float Env(float t, float attack, float decay) => t < 0f ? 0f : t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

    private AudioStreamWav Build(float seconds, Func<float, float> f)
    {
        int n = (int)(seconds * Rate);
        var bytes = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            short s = (short)(Mathf.Clamp(f(t), -1f, 1f) * short.MaxValue * 0.8f);
            bytes[i * 2] = (byte)(s & 0xff);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = bytes };
    }

    private void Play(string key, Vector3 position, float volumeDb = 0f, float pitch = 1f)
    {
        if (!_streams.TryGetValue(key, out var stream)) return;
        AudioStreamPlayer3D? player = null;
        foreach (var p in _pool) if (!p.Playing) { player = p; break; }
        player ??= _pool[0];
        player.Stream = stream;
        player.GlobalPosition = position;
        player.VolumeDb = volumeDb;
        player.PitchScale = pitch * (0.94f + (float)_rnd.NextDouble() * 0.12f);
        player.Play();
    }

    public void Gunshot(string kind, Vector3 position) => Play(kind == "revolver" ? "revolver" : "rifle", position, 4f);
    public void Click(Vector3 position) => Play("click", position, -6f);
    public void Hit(Vector3 position) => Play("hit", position, -2f);
    public void Step(Vector3 position) => Play("step", position, -14f);
    public void Hoof(Vector3 position, bool gallop) => Play("hoof", position, gallop ? -4f : -10f, gallop ? 1.1f : 0.9f);
    public void Whistle(Vector3 position) => Play("whistle", position, 0f);
    public void Coins(Vector3 position) => Play("coins", position, -4f);

    public void RainLevel(float intensity)
    {
        if (intensity < 0.02f) { if (_rainPlayer.Playing) _rainPlayer.Stop(); return; }
        if (!_rainPlayer.Playing) _rainPlayer.Play();
        _rainPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(intensity * 0.45f, 0.001f, 1f));
    }

    public void Thunder() => _thunderPlayer.Play();

    /// <summary>A looping engine sound for one vehicle; the vehicle owns and pitches it.</summary>
    public AudioStreamPlayer3D EngineLoop() => new() { Stream = _streams["engine"], MaxDistance = 120f, UnitSize = 6f, VolumeDb = -6f, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance };

    public void Horn(Vector3 position) => Play("horn", position, 2f);
}
