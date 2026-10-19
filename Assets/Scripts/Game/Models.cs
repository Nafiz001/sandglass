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
            if (Application.isPlaying) Object.Destroy(g.GetComponent<Collider>());
            else Object.DestroyImmediate(g.GetComponent<Collider>()); // editor previews
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
            var glow = Fx.Tint("M_Lit", Accent, Accent * 1.6f);
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
                var lens = Fx.Tint("M_Lit", new Color(0.05f, 0.08f, 0.1f));
                lens.SetFloat("_Smoothness", 0.95f);
                Part(PrimitiveType.Cube, root, new Vector3(0, 0.09f, -0.047f), new Vector3(0.036f, 0.036f, 0.004f), lens);
                var dot = Fx.Tint("M_Lit", new Color(1f, 0.3f, 0.2f), new Color(6f, 1.2f, 0.6f));
                Part(PrimitiveType.Cube, root, new Vector3(0, 0.09f, -0.05f), new Vector3(0.004f, 0.004f, 0.002f), dot);
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

        /// <summary>A capsule spanning two points, for limbs.</summary>
        static void Limb(Transform parent, Vector3 a, Vector3 b, float radius, Material m)
        {
            var g = Part(PrimitiveType.Capsule, parent, (a + b) * 0.5f, Vector3.one, m);
            var d = b - a;
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            g.transform.localScale = new Vector3(radius * 2f, d.magnitude * 0.5f + radius * 0.5f, radius * 2f);
        }

        /// <summary>Builds a soldier standing on its root (feet at y = 0).</summary>
        public static AvatarRig Soldier(Transform parent, Color accent)
        {
            var armor = Fx.Tint("M_Lit", Armor);
            armor.SetFloat("_Smoothness", 0.45f);
            var cloth = Fx.Tint("M_Lit", Cloth);
            var wrap = Fx.Tint("M_Lit", new Color(0.74f, 0.62f, 0.46f));
            var skin = Fx.Tint("M_Lit", new Color(0.55f, 0.38f, 0.28f));
            var visor = Fx.Tint("M_Lit", accent, accent * 2.2f);

            var rig = new AvatarRig();
            rig.Root = new GameObject("Soldier").transform;
            rig.Root.SetParent(parent, false);

            rig.LegL = Pivot("LegL", rig.Root, new Vector3(-0.13f, 0.92f, 0));
            rig.LegR = Pivot("LegR", rig.Root, new Vector3(0.13f, 0.92f, 0));
            foreach (var leg in new[] { rig.LegL, rig.LegR })
            {
                Limb(leg, new Vector3(0, -0.05f, 0), new Vector3(0, -0.48f, 0.02f), 0.115f, cloth);
                Limb(leg, new Vector3(0, -0.5f, 0.02f), new Vector3(0, -0.82f, -0.02f), 0.1f, cloth);
                Part(PrimitiveType.Cube, leg, new Vector3(0, -0.5f, 0.07f), new Vector3(0.17f, 0.15f, 0.08f), armor);
                Part(PrimitiveType.Cube, leg, new Vector3(0, -0.86f, 0.05f), new Vector3(0.18f, 0.13f, 0.3f), armor);
            }
            Part(PrimitiveType.Cube, rig.Root, new Vector3(0, 0.97f, 0), new Vector3(0.42f, 0.2f, 0.26f), armor);
            Part(PrimitiveType.Cube, rig.Root, new Vector3(0.22f, 0.92f, 0.04f), new Vector3(0.08f, 0.14f, 0.14f), cloth);

            rig.Torso = Pivot("Torso", rig.Root, new Vector3(0, 1.04f, 0));
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(0, 0.25f, 0), new Vector3(0.46f, 0.5f, 0.28f), cloth);
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(0, 0.32f, 0.11f), new Vector3(0.5f, 0.36f, 0.11f), armor);
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(0, 0.3f, -0.19f), new Vector3(0.36f, 0.4f, 0.14f), armor);
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(0, 0.28f, 0.17f), new Vector3(0.34f, 0.025f, 0.01f), visor);
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(-0.13f, 0.12f, 0.15f), new Vector3(0.1f, 0.12f, 0.06f), armor);
            Part(PrimitiveType.Cube, rig.Torso, new Vector3(0.01f, 0.12f, 0.15f), new Vector3(0.1f, 0.12f, 0.06f), armor);
            Part(PrimitiveType.Cylinder, rig.Torso, new Vector3(0, 0.53f, 0), new Vector3(0.27f, 0.06f, 0.27f), wrap);

            // Shoulders and arms holding the rifle at the chest.
            var shL = new Vector3(-0.29f, 0.45f, 0);
            var shR = new Vector3(0.29f, 0.45f, 0);
            Part(PrimitiveType.Sphere, rig.Torso, shL, new Vector3(0.2f, 0.17f, 0.22f), armor);
            Part(PrimitiveType.Sphere, rig.Torso, shR, new Vector3(0.2f, 0.17f, 0.22f), armor);
            var gunPivot = Pivot("GunPivot", rig.Torso, new Vector3(0.1f, 0.33f, 0.26f));
            var grip = gunPivot.localPosition + new Vector3(0, -0.1f, -0.13f);
            var fore = gunPivot.localPosition + new Vector3(0, -0.02f, 0.3f);
            var elbowR = new Vector3(0.3f, 0.2f, 0.06f);
            var elbowL = new Vector3(-0.2f, 0.18f, 0.24f);
            Limb(rig.Torso, shR, elbowR, 0.075f, cloth);
            Limb(rig.Torso, elbowR, grip, 0.065f, cloth);
            Limb(rig.Torso, shL, elbowL, 0.075f, cloth);
            Limb(rig.Torso, elbowL, fore, 0.065f, cloth);
            Part(PrimitiveType.Sphere, rig.Torso, grip, Vector3.one * 0.085f, armor);
            Part(PrimitiveType.Sphere, rig.Torso, fore, Vector3.one * 0.085f, armor);

            rig.Head = Pivot("Head", rig.Torso, new Vector3(0, 0.58f, 0));
            Part(PrimitiveType.Sphere, rig.Head, new Vector3(0, 0.1f, 0.01f), new Vector3(0.21f, 0.24f, 0.22f), skin);
            Part(PrimitiveType.Cylinder, rig.Head, new Vector3(0, 0.03f, 0.01f), new Vector3(0.23f, 0.05f, 0.23f), wrap);
            Part(PrimitiveType.Sphere, rig.Head, new Vector3(0, 0.17f, -0.01f), new Vector3(0.3f, 0.24f, 0.31f), armor);
            Part(PrimitiveType.Cube, rig.Head, new Vector3(0, 0.13f, 0.12f), new Vector3(0.22f, 0.06f, 0.05f), visor);
            Part(PrimitiveType.Cube, rig.Head, new Vector3(0.13f, 0.13f, 0.02f), new Vector3(0.03f, 0.1f, 0.12f), armor);
            Part(PrimitiveType.Cube, rig.Head, new Vector3(-0.13f, 0.13f, 0.02f), new Vector3(0.03f, 0.1f, 0.12f), armor);

            rig.Muzzle = Gun(gunPivot, 0, false);
            rig.Gun = gunPivot;
            return rig;
        }
    }
}
