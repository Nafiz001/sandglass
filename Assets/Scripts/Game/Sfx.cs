using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandglass.Game
{
    /// <summary>
    /// Every sound in the game is synthesised here at startup, so the build
    /// carries no audio files.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly List<AudioSource> pool = new List<AudioSource>();
        static Transform root;
        static AudioSource wind;
        static System.Random rng = new System.Random(3);

        public static float Volume = 0.8f;

        static float Noise() => (float)(rng.NextDouble() * 2 - 1);

        static AudioClip Make(string name, float seconds, Func<float, float> f, float gain = 1f)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                data[i] = f(i / (float)Rate);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            for (int i = 0; i < n; i++) data[i] = data[i] / peak * gain;
            c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            clips[name] = c;
            return c;
        }

        // One-pole low-pass state shared by a generator while it runs.
        sealed class Lp
        {
            float y;
            readonly float a;
            public Lp(float cutoff) { a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate); }
            public float Run(float x) => y += a * (x - y);
        }

        public static AudioClip Rifle()
        {
            var body = new Lp(1800f);
            var low = new Lp(220f);
            return Make("rifle", 0.32f, t =>
            {
                float n = Noise();
                float crack = n * Mathf.Exp(-t * 90f);
                float blast = body.Run(n) * Mathf.Exp(-t * 16f) * 1.6f;
                float thump = Mathf.Sin(2 * Mathf.PI * 62f * t) * Mathf.Exp(-t * 24f) * 0.9f + low.Run(n) * Mathf.Exp(-t * 10f) * 2f;
                return crack * 0.7f + blast + thump;
            }, 0.9f);
        }

        public static AudioClip Pistol()
        {
            var body = new Lp(2600f);
            return Make("pistol", 0.22f, t =>
            {
                float n = Noise();
                return n * Mathf.Exp(-t * 120f) * 0.8f + body.Run(n) * Mathf.Exp(-t * 26f) * 1.4f
                       + Mathf.Sin(2 * Mathf.PI * 95f * t) * Mathf.Exp(-t * 30f) * 0.6f;
            }, 0.85f);
        }

        public static AudioClip HitMarker() => Make("hit", 0.07f, t =>
            Mathf.Sin(2 * Mathf.PI * 1900f * t) * Mathf.Exp(-t * 60f), 0.5f);

        public static AudioClip Headshot() => Make("head", 0.35f, t =>
            (Mathf.Sin(2 * Mathf.PI * 2350f * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 3520f * t)) * Mathf.Exp(-t * 12f), 0.55f);

        public static AudioClip Hurt()
        {
            var lp = new Lp(500f);
            return Make("hurt", 0.25f, t => lp.Run(Noise()) * Mathf.Exp(-t * 14f) + Mathf.Sin(2 * Mathf.PI * 110f * t) * Mathf.Exp(-t * 18f) * 0.5f, 0.8f);
        }

        public static AudioClip Click() => Make("click", 0.04f, t => Noise() * Mathf.Exp(-t * 300f), 0.5f);

        public static AudioClip Reload() => Make("reload", 0.9f, t =>
        {
            float a = t < 0.06f ? Noise() * Mathf.Exp(-t * 160f) : 0f;
            float b = t > 0.55f && t < 0.62f ? Noise() * Mathf.Exp(-(t - 0.55f) * 160f) : 0f;
            float c = t > 0.68f ? Noise() * Mathf.Exp(-(t - 0.68f) * 220f) * 0.8f : 0f;
            return a + b + c;
        }, 0.45f);

        public static AudioClip Beep(bool high) => Make(high ? "go" : "beep", high ? 0.4f : 0.15f, t =>
            Mathf.Sin(2 * Mathf.PI * (high ? 1318f : 880f) * t) * Mathf.Exp(-t * (high ? 6f : 18f)), 0.45f);

        public static AudioClip Sting(bool win) => Make(win ? "win" : "lose", 1.6f, t =>
        {
            float[] notes = win ? new[] { 523f, 659f, 784f, 1046f } : new[] { 392f, 349f, 311f, 262f };
            int i = Mathf.Min((int)(t / 0.18f), 3);
            float local = t - i * 0.18f;
            float env = Mathf.Exp(-local * (i == 3 ? 2.2f : 9f));
            float f = notes[i];
            return (Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * f * t)) * env;
        }, 0.5f);

        static AudioClip WindLoop()
        {
            var lp1 = new Lp(380f);
            var lp2 = new Lp(900f);
            const float len = 8f;
            return Make("wind", len, t =>
            {
                float gust = 0.55f + 0.3f * Mathf.Sin(2 * Mathf.PI * t / len) + 0.15f * Mathf.Sin(2 * Mathf.PI * 3 * t / len + 1.3f);
                float hiss = lp2.Run(Noise()) * 0.25f * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 2 * t / len));
                return lp1.Run(Noise()) * gust + hiss;
            }, 0.35f);
        }

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    root = new GameObject("Audio").transform;
                    UnityEngine.Object.DontDestroyOnLoad(root.gameObject);
                }
                return root;
            }
        }

        /// <summary>Plays a clip; with a position it is 3D, otherwise 2D.</summary>
        public static void Play(AudioClip clip, float volume = 1f, Vector3? at = null, float pitchJitter = 0.05f)
        {
            AudioSource src = null;
            foreach (var s in pool)
                if (!s.isPlaying) { src = s; break; }
            if (src == null)
            {
                if (pool.Count >= 24) src = pool[0];
                else
                {
                    var go = new GameObject("Sfx");
                    go.transform.SetParent(Root, false);
                    src = go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.rolloffMode = AudioRolloffMode.Linear;
                    src.minDistance = 4f;
                    src.maxDistance = 90f;
                    pool.Add(src);
                }
            }
            src.spatialBlend = at.HasValue ? 1f : 0f;
            if (at.HasValue) src.transform.position = at.Value;
            src.clip = clip;
            src.volume = volume * Volume;
            src.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            src.Play();
        }

        public static void StartWind()
        {
            if (wind != null) return;
            var go = new GameObject("Wind");
            go.transform.SetParent(Root, false);
            wind = go.AddComponent<AudioSource>();
            wind.clip = WindLoop();
            wind.loop = true;
            wind.volume = 0.22f * Volume;
            wind.Play();
        }
    }
}
