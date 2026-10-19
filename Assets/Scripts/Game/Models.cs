using UnityEngine;

namespace Sandglass.Game
{
    /// <summary>Handles to the moving parts of a built character.</summary>
    public class AvatarRig
    {
        public Transform Root, Torso, Head, LegL, LegR, Gun, Muzzle;
    }

    /// <summary>
    /// Builds the stylised characters and guns out of primitives, so the game
    /// ships without imported meshes and every part can be animated in code.
    /// </summary>
    public static class Models
    {
        public static readonly Color Armor = new Color(0.17f, 0.19f, 0.22f);
        public static readonly Color Cloth = new Color(0.16f, 0.24f, 0.32f);
        public static readonly Color Gunmetal = new Color(0.13f, 0.13f, 0.14f);
        public static readonly Color Polymer = new Color(0.55f, 0.45f, 0.33f);
        public static readonly Color Accent = new Color(0.25f, 0.9f, 1f);

        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            return g;
        }

        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        /// <summary>Builds a weapon pointing down +Z. Returns the muzzle point.</summary>
        public static Transform Gun(Transform parent, int weapon, bool firstPerson)
        {
            var metal = Fx.Tint("M_Lit", Gunmetal);
            metal.SetFloat("_Smoothness", 0.55f);
            metal.SetFloat("_Metallic", 0.6f);
            var poly = Fx.Tint("M_Lit", Polymer);
            var glow = Fx.Tint("M_Lit", Accent, Accent * 3f);
            var root = new GameObject(weapon == 0 ? "Rifle" : "Pistol").transform;
            root.SetParent(parent, false);
            Transform muzzle;
            if (weapon == 0)
            {
                Part(PrimitiveType.Cube, root, new Vector3(0, 0, 0), new Vector3(0.07f, 0.11f, 0.42f), metal);
                Part(PrimitiveType.Cube, root, new Vector3(0, 0.005f, 0.33f), new Vector3(0.068f, 0.085f, 0.3f), poly);
                Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0.56f), new Vector3(0.026f, 0.14f, 0.026f), metal, new Vector3(90, 0, 0));
                Part(PrimitiveType.Cube, root, new Vector3(0, 0.02f, 0.7f), new Vector3(0.042f, 0.042f, 0.07f), metal);
                Part(PrimitiveType.Cube, root, new Vector3(0, -0.13f, 0.08f), new Vector3(0.05f, 0.17f, 0.08f), metal, new Vector3(12, 0, 0));
                Part(PrimitiveType.Cube, root, new Vector3(0, -0.1f, -0.13f), new Vector3(0.046f, 0.12f, 0.05f), poly, new Vector3(-15, 0, 0));
                Part(PrimitiveType.Cube, root, new Vector3(0, -0.02f, -0.31f), new Vector3(0.056f, 0.1f, 0.22f), poly);
                Part(PrimitiveType.Cube, root, new Vector3(0, 0.09f, 0.02f), new Vector3(0.05f, 0.05f, 0.13f), metal);
                Part(PrimitiveType.Cube, root, new Vector3(0, 0.09f, -0.047f), new Vector3(0.034f, 0.034f, 0.004f), glow);
                Part(PrimitiveType.Cube, root, new Vector3(0.036f, 0.01f, 0.33f), new Vector3(0.004f, 0.012f, 0.22f), glow);
                Part(PrimitiveType.Cube, root, new Vector3(-0.036f, 0.01f, 0.33f), new Vector3(0.004f, 0.012f, 0.22f), glow);
                muzzle = Pivot("Muzzle", root, new Vector3(0, 0.02f, 0.75f));
            }
            else
            {
                Part(PrimitiveType.Cube, root, new Vector3(0, 0.03f, 0.04f), new Vector3(0.042f, 0.05f, 0.21f), metal);
                Part(PrimitiveType.Cube, root, new Vector3(0, -0.006f, 0.03f), new Vector3(0.04f, 0.035f, 0.17f), poly);
                Part(PrimitiveType.Cube, root, new Vector3(0, -0.075f, -0.045f), new Vector3(0.04f, 0.115f, 0.052f), poly, new Vector3(-12, 0, 0));
                Part(PrimitiveType.Cube, root, new Vector3(0, 0.058f, 0.12f), new Vector3(0.008f, 0.012f, 0.008f), glow);
                Part(PrimitiveType.Cube, root, new Vector3(0.022f, 0.03f, 0.04f), new Vector3(0.003f, 0.01f, 0.15f), glow);
                muzzle = Pivot("Muzzle", root, new Vector3(0, 0.03f, 0.16f));
            }
            if (firstPerson)
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return muzzle;
        }

        /// <summary>Builds a soldier standing on its root (feet at y = 0).</summary>
        public static AvatarRig Soldier(Transform parent, Color accent)
        {
            var armor = Fx.Tint("M_Lit", Armor);
            armor.SetFloat("_Smoothness", 0.4f);
            var cloth = Fx.Tint("M_Lit", Cloth);
            var wrap = Fx.Tint("M_Lit", new Color(0.72f, 0.6f, 0.44f));
            var visor = Fx.Tint("M_Lit", accent, accent * 4f);

            var rig = new AvatarRig();
            rig.Root = new GameObject("Soldier").transform;
            rig.Root.SetParent(parent, false);

            rig.LegL = Pivot("LegL", rig.Root, new Vector3(-0.12f, 0.9f, 0));
            rig.LegR = Pivot("LegR", rig.Root, new Vector3(0.12f, 0.9f, 0));
            foreach (var leg in new[] { rig.LegL, rig.LegR })
            {
                Part(PrimitiveType.Capsule, leg, new Vector3(0, -0.42f, 0), new Vector3(0.18f, 0.42f, 0.18f), cloth);
                Part(PrimitiveType.Cube, leg, new Vector3(0, -0.47f, 0.02f), new Vector3(0.17f, 0.12f, 0.16f), armor);
                Part(PrimitiveType.Cube, leg, new Vector3(0, -0.84f, 0.05f), new Vector3(0.16f, 0.12f, 0.26f), armor);
            }
            Part(PrimitiveType.Cube, rig.Root, new Vector3(0, 0.96f, 0), new Vector3(0.38f, 0.18f, 0.24f), armor);

            rig.Torso = Pivot("Torso", rig.Root, new Vector3(0, 1.02f, 0));
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(0, 0.26f, 0), new Vector3(0.42f, 0.5f, 0.26f), cloth);
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(0, 0.33f, 0.1f), new Vector3(0.45f, 0.32f, 0.1f), armor);
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(0, 0.3f, -0.18f), new Vector3(0.32f, 0.34f, 0.13f), armor);
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(0, 0.3f, 0.156f), new Vector3(0.3f, 0.02f, 0.01f), visor);
            Part(PrimitiveType.Cylinder, rig.Torso, new Vector3(0, 0.53f, 0), new Vector3(0.24f, 0.05f, 0.24f), wrap);
            // Shoulders and arms reaching to the gun.
            Part(PrimitiveType.Sphere, rig.Torso, new Vector3(-0.25f, 0.46f, 0), Vector3.one * 0.17f, armor);
            Part(PrimitiveType.Sphere, rig.Torso, new Vector3(0.25f, 0.46f, 0), Vector3.one * 0.17f, armor);
            Part(PrimitiveType.Capsule, rig.Torso, new Vector3(0.2f, 0.36f, 0.16f), new Vector3(0.11f, 0.2f, 0.11f), cloth, new Vector3(65, -20, 0));
            Part(PrimitiveType.Capsule, rig.Torso, new Vector3(-0.17f, 0.34f, 0.24f), new Vector3(0.11f, 0.24f, 0.11f), cloth, new Vector3(75, 35, 0));

            rig.Head = Pivot("Head", rig.Torso, new Vector3(0, 0.58f, 0));
            Part(PrimitiveType.Sphere, rig.Head, new Vector3(0, 0.12f, 0), Vector3.one * 0.24f, wrap);
            Part(PrimitiveType.Sphere, rig.Head, new Vector3(0, 0.17f, -0.015f), new Vector3(0.29f, 0.23f, 0.3f), armor);
            Part(PrimitiveType.Cube, rig.Head, new Vector3(0, 0.12f, 0.115f), new Vector3(0.21f, 0.055f, 0.04f), visor);

            var gunPivot = Pivot("GunPivot", rig.Torso, new Vector3(0.06f, 0.32f, 0.3f));
            rig.Muzzle = Gun(gunPivot, 0, false);
            rig.Gun = gunPivot;
            return rig;
        }
    }
}
