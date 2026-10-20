using System.Collections.Generic;
using UnityEngine;

namespace Sandglass.Game
{
    /// <summary>
    /// An animated opponent built from Quaternius's Toon Shooter characters
    /// (CC0), imported with glTFast as legacy animation clips. The character
    /// carries every gun in the kit on its hand; only the current one shows.
    /// </summary>
    public class CharacterView
    {
        const string Prefix = "CharacterArmature|";
        public const float Height = 1.8f;

        static readonly string[] GunNodes = { "AK", "Pistol" };

        public readonly Transform Root;
        readonly Animation anim;
        readonly Transform torso;
        readonly Dictionary<string, Renderer> guns = new Dictionary<string, Renderer>();
        string playing;
        float oneShotUntil;
        int weapon = -1;

        public CharacterView(Transform parent, bool enemy)
        {
            var prefab = Resources.Load<GameObject>("Models/" + (enemy ? "Character_Enemy" : "Character_Soldier"));
            Root = Object.Instantiate(prefab, parent, false).transform;
            Root.name = "Character";
            Models.UseLitMaterials(Root);
            anim = Root.GetComponentInChildren<Animation>();
            if (anim != null) anim.playAutomatically = false;

            foreach (var t in Root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Torso") torso = t;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                bool isGun = false;
                foreach (var g in GunNodes) if (t.name == g) { guns[g] = r; isGun = true; }
                // Every other weapon in the kit is hidden; only body parts and our guns remain.
                if (!isGun && IsKitWeapon(t.name)) t.gameObject.SetActive(false);
            }

            // Scale to a standing height measured in the idle pose.
            Play("Idle", 0f);
            if (anim != null) anim.Sample();
            var b = Bounds();
            if (b.size.y > 0.01f) Root.localScale = Vector3.one * (Height / b.size.y);
            SetWeapon(0);
        }

        static bool IsKitWeapon(string n) =>
            n == "Revolver" || n == "Sniper" || n == "Revolver_Small" || n == "SMG" || n == "GrenadeLauncher" ||
            n == "ShortCannon" || n == "Shotgun" || n == "Sniper_2" || n == "RocketLauncher" || n == "Shovel" ||
            n == "Knife_1" || n == "Knife_2";

        Bounds Bounds()
        {
            var rs = Root.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (rs.Length == 0) return new Bounds(Root.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public void SetWeapon(int w)
        {
            if (w == weapon) return;
            weapon = w;
            foreach (var kv in guns) kv.Value.gameObject.SetActive(kv.Key == GunNodes[Mathf.Clamp(w, 0, 1)]);
        }

        /// <summary>The far end of the visible gun along the character's facing.</summary>
        public Vector3 Muzzle
        {
            get
            {
                if (!guns.TryGetValue(GunNodes[Mathf.Clamp(weapon, 0, 1)], out var r) || r == null)
                    return Root.position + Vector3.up * 1.4f + Root.forward * 0.5f;
                var b = r.bounds;
                var f = Root.forward;
                var e = b.extents;
                return b.center + f * (Mathf.Abs(f.x) * e.x + Mathf.Abs(f.y) * e.y + Mathf.Abs(f.z) * e.z);
            }
        }

        void Play(string clip, float fade)
        {
            if (anim == null || playing == clip) return;
            var state = anim[Prefix + clip];
            if (state == null) return;
            playing = clip;
            if (fade <= 0f) anim.Play(Prefix + clip);
            else anim.CrossFade(Prefix + clip, fade);
        }

        bool Has(string clip) => anim != null && anim[Prefix + clip] != null;

        public void Hit()
        {
            if (!Has("HitReact") || playing == "Death") return;
            var s = anim[Prefix + "HitReact"];
            s.wrapMode = WrapMode.Once;
            playing = null;
            anim.CrossFade(Prefix + "HitReact", 0.05f);
            playing = "HitReact";
            oneShotUntil = Time.time + s.length * 0.7f;
        }

        public void Die()
        {
            if (!Has("Death")) return;
            var s = anim[Prefix + "Death"];
            s.wrapMode = WrapMode.ClampForever;
            playing = null;
            Play("Death", 0.1f);
        }

        public void Revive()
        {
            playing = null;
            oneShotUntil = 0f;
            Play("Idle_Shoot", 0f);
        }

        /// <param name="speed">horizontal speed in m/s</param>
        /// <param name="pitch">aim pitch in degrees, positive looking down</param>
        public void Animate(float speed, bool alive)
        {
            if (!alive || anim == null) return;
            if (Time.time < oneShotUntil) return;
            string loop = speed > 0.6f ? (Has("Run_Shoot") ? "Run_Shoot" : "Run_Gun") : "Idle_Shoot";
            if (anim[Prefix + loop] != null) anim[Prefix + loop].wrapMode = WrapMode.Loop;
            Play(loop, 0.15f);
            if (loop != "Idle_Shoot" && anim[Prefix + loop] != null)
                anim[Prefix + loop].speed = Mathf.Clamp(speed / 5f, 0.6f, 1.4f);
        }

        /// <summary>Bends the torso to the aim pitch after animation runs.</summary>
        public void AimPitch(float pitch)
        {
            if (torso == null || playing == "Death") return;
            torso.rotation = Quaternion.AngleAxis(pitch * 0.7f, Root.right) * torso.rotation;
        }
    }
}
