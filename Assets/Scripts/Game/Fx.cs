using System.Collections.Generic;
using UnityEngine;

namespace Sandglass.Game
{
    /// <summary>
    /// Pooled visual effects: bullet tracers, impact puffs and muzzle flashes.
    /// Materials live in Resources/Materials so the WebGL build keeps their
    /// shaders.
    /// </summary>
    public static class Fx
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Transform root;
        static readonly List<TracerLine> tracers = new List<TracerLine>();
        static ParticleSystem dust, sparks;
        static Texture2D softDot;

        public static Material Mat(string name)
        {
            if (!mats.TryGetValue(name, out var m) || m == null)
            {
                m = Resources.Load<Material>("Materials/" + name);
                if (m == null) Debug.LogError("[Fx] Missing material Resources/Materials/" + name);
                mats[name] = m;
            }
            return m;
        }

        /// <summary>A coloured copy of a base material (lit or unlit).</summary>
        public static Material Tint(string baseName, Color color, Color? emission = null)
        {
            var m = new Material(Mat(baseName));
            m.SetColor("_BaseColor", color);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            return m;
        }

        static Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("FX").transform;
                return root;
            }
        }

        class TracerLine
        {
            public LineRenderer line;
            public float born, life;
            public Vector3 a, b;
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.03f)
        {
            TracerLine t = null;
            foreach (var x in tracers)
                if (!x.line.gameObject.activeSelf) { t = x; break; }
            if (t == null)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(Root, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 2;
                lr.sharedMaterial = Mat("FX_Tracer");
                t = new TracerLine { line = lr };
                tracers.Add(t);
            }
            t.line.gameObject.SetActive(true);
            t.a = from; t.b = to; t.born = Time.time;
            t.life = Mathf.Clamp(Vector3.Distance(from, to) / 400f, 0.05f, 0.12f);
            t.line.startWidth = width * 0.4f;
            t.line.endWidth = width;
            t.line.startColor = new Color(color.r, color.g, color.b, 0f);
            t.line.endColor = color;
            t.line.SetPosition(0, from);
            t.line.SetPosition(1, to);
        }

        /// <summary>Called once per frame by the game client.</summary>
        public static void Tick()
        {
            foreach (var t in tracers)
            {
                if (!t.line.gameObject.activeSelf) continue;
                float k = (Time.time - t.born) / t.life;
                if (k >= 1f) { t.line.gameObject.SetActive(false); continue; }
                // The streak's tail chases its head towards the target.
                t.line.SetPosition(0, Vector3.Lerp(t.a, t.b, Mathf.Clamp01(k * 1.4f - 0.2f)));
                t.line.SetPosition(1, Vector3.Lerp(t.a, t.b, Mathf.Clamp01(k * 1.4f + 0.3f)));
            }
        }

        public static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int n = 32;
                softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        softDot.SetPixel(x, y, new Color(1, 1, 1, a * a));
                    }
                softDot.Apply();
                return softDot;
            }
        }

        static ParticleSystem MakeSystem(string name, Color color, float speed, float size, float life, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            var m = new Material(Mat("FX_Particle"));
            m.SetTexture("_BaseMap", SoftDot);
            r.sharedMaterial = m;
            return ps;
        }

        /// <summary>Sunlit dust drifting on the wind around the viewer.</summary>
        public static void AmbientDust(Transform follow)
        {
            var ps = MakeSystem("AmbientDust", new Color(1.3f, 1.05f, 0.8f, 0.55f), 0.2f, 0.07f, 9f, -0.01f);
            ps.transform.SetParent(follow, false);
            ps.transform.localPosition = Vector3.zero;
            ps.transform.localRotation = Quaternion.identity;
            var main = ps.main;
            main.loop = true;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 45f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 9f, 36f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.3f;
            var col = ps.colorOverLifetime;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = false;
            ps.Play();
        }

        /// <summary>Puff of dust where a bullet hits stone or sand.</summary>
        public static void Impact(Vector3 point, Vector3 normal)
        {
            if (dust == null) dust = MakeSystem("Dust", new Color(0.86f, 0.72f, 0.55f, 0.85f), 2.2f, 0.45f, 0.9f, 0.15f);
            Emit(dust, point, normal, 10);
        }

        /// <summary>Bright sparks when a bullet hits a player.</summary>
        public static void Blood(Vector3 point, Vector3 normal)
        {
            if (sparks == null) sparks = MakeSystem("Sparks", new Color(4f, 1.2f, 0.4f, 1f), 6f, 0.12f, 0.35f, 1.2f);
            Emit(sparks, point, normal, 14);
        }

        static void Emit(ParticleSystem ps, Vector3 point, Vector3 normal, int count)
        {
            ps.transform.position = point + normal * 0.03f;
            ps.transform.rotation = Quaternion.LookRotation(normal == Vector3.zero ? Vector3.up : normal);
            ps.Emit(count);
        }
    }
}
