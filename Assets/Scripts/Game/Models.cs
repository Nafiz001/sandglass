using UnityEngine;

namespace Sandglass.Game
{
    /// <summary>
    /// Builds props at runtime: primitive parts for effects, and the
    /// first-person guns from Quaternius's Toon Shooter kit (CC0).
    /// </summary>
    public static class Models
    {
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(type);
            if (Application.isPlaying) Object.Destroy(g.GetComponent<Collider>());
            else Object.DestroyImmediate(g.GetComponent<Collider>()); // editor previews
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static readonly System.Collections.Generic.Dictionary<Material, Material> litCopies =
            new System.Collections.Generic.Dictionary<Material, Material>();

        /// <summary>
        /// Swaps glTFast's imported materials for the game's URP Lit material in
        /// the same colour, matte rather than metallic, so the kit matches the
        /// arena and the build needs no extra shaders.
        /// </summary>
        public static void UseLitMaterials(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (!litCopies.TryGetValue(src, out var lit) || lit == null)
                    {
                        var c = src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor") : Color.grey;
                        lit = Fx.Tint("M_Lit", c);
                        lit.name = src.name;
                        lit.SetFloat("_Metallic", 0f);
                        lit.SetFloat("_Smoothness", 0.25f);
                        litCopies[src] = lit;
                    }
                    mats[i] = lit;
                }
                r.sharedMaterials = mats;
            }
        }

        struct GunSpec
        {
            public string Resource;
            public Vector3 Euler;      // turns the model's barrel to +Z, top to +Y
            public float Length;       // metres, muzzle to stock
            public float BarrelHeight; // muzzle height as a fraction of the gun's height
        }

        static readonly GunSpec[] Specs =
        {
            new GunSpec { Resource = "Models/Ak47", Euler = new Vector3(0, -90, 0), Length = 0.8f, BarrelHeight = 0.72f },
            new GunSpec { Resource = "Models/Pistol", Euler = new Vector3(0, -90, 0), Length = 0.24f, BarrelHeight = 0.8f },
        };

        /// <summary>
        /// Builds a first-person gun under holder: barrel along +Z, scaled to
        /// its real length, top edge at y = 0 and grip near the origin.
        /// Returns the muzzle point.
        /// </summary>
        public static Transform KitGun(Transform holder, int weapon)
        {
            var spec = Specs[Mathf.Clamp(weapon, 0, Specs.Length - 1)];
            var gun = Object.Instantiate(Resources.Load<GameObject>(spec.Resource), holder, false).transform;
            gun.name = spec.Resource.Substring(7);
            gun.localRotation = Quaternion.Euler(spec.Euler);
            UseLitMaterials(gun);

            var b = LocalBounds(holder, gun);
            if (b.size.z > 1e-6f) gun.localScale = Vector3.one * (spec.Length / b.size.z);
            b = LocalBounds(holder, gun);
            // Centre left-right, top edge at 0, and a third of the gun behind the grip point.
            gun.localPosition -= new Vector3(b.center.x, b.max.y, b.min.z + b.size.z * 0.35f);
            b = LocalBounds(holder, gun);

            foreach (var r in gun.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(holder, false);
            muzzle.localPosition = new Vector3(0f, b.min.y + b.size.y * spec.BarrelHeight, b.max.z);
            return muzzle;
        }

        /// <summary>Bounds of every mesh under root, in holder's space.</summary>
        static Bounds LocalBounds(Transform holder, Transform root)
        {
            bool any = false;
            var result = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = holder.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }
    }
}
