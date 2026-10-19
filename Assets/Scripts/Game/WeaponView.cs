using UnityEngine;

namespace Sandglass.Game
{
    /// <summary>
    /// The first-person gun: procedural bob, sway, recoil kick, aim-down-
    /// sights, reload and switch animations, and the muzzle flash.
    /// </summary>
    public class WeaponView : MonoBehaviour
    {
        static readonly Vector3 HipPos = new Vector3(0.16f, -0.135f, 0.36f);
        static readonly Vector3[] AdsPos = { new Vector3(0f, -0.09f, 0.24f), new Vector3(0f, -0.088f, 0.3f) };

        Transform sway, kick;
        readonly Transform[] guns = new Transform[2];
        readonly Transform[] muzzles = new Transform[2];
        Transform flash;
        Light flashLight;
        float flashUntil;

        int current;
        float kickZ, kickPitch, kickVelZ, kickVelPitch;
        float bobPhase;
        Vector2 swayOffset;
        float adsBlend;
        float reloadStart = -10f, reloadLength = 1f;
        float switchStart = -10f;

        public Transform Muzzle => muzzles[current];

        public void Build()
        {
            sway = new GameObject("Sway").transform;
            sway.SetParent(transform, false);
            kick = new GameObject("Kick").transform;
            kick.SetParent(sway, false);
            for (int i = 0; i < 2; i++)
            {
                var holder = new GameObject("Holder" + i).transform;
                holder.SetParent(kick, false);
                muzzles[i] = Models.Gun(holder, i, true);
                guns[i] = holder;
            }
            flash = new GameObject("Flash").transform;
            var flashMat = new Material(Fx.Mat("FX_Flash"));
            flashMat.SetTexture("_BaseMap", Fx.SoftDot);
            var quadA = Models.Part(PrimitiveType.Quad, flash, Vector3.zero, new Vector3(0.22f, 0.22f, 1), flashMat);
            var quadB = Models.Part(PrimitiveType.Quad, flash, new Vector3(0, 0, 0.04f), new Vector3(0.09f, 0.3f, 1), flashMat, new Vector3(0, 90, 0));
            quadA.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadB.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flashLight = flash.gameObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = 6f;
            flashLight.intensity = 4f;
            flashLight.color = new Color(1f, 0.75f, 0.4f);
            flashLight.shadows = LightShadows.None;
            flash.gameObject.SetActive(false);
            Show(0);
        }

        public void Show(int weapon)
        {
            current = weapon;
            for (int i = 0; i < 2; i++) guns[i].gameObject.SetActive(i == weapon);
            switchStart = Time.time;
        }

        public void Fire(float strength)
        {
            kickVelZ -= 2.2f * strength;
            kickVelPitch -= 90f * strength;
            flash.SetParent(Muzzle, false);
            flash.localPosition = new Vector3(0, 0, 0.03f);
            flash.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 90f));
            flash.gameObject.SetActive(true);
            flashUntil = Time.time + 0.045f;
        }

        public void Reload(float seconds)
        {
            reloadStart = Time.time;
            reloadLength = seconds;
        }

        public void CancelReload() => reloadStart = -10f;

        /// <param name="speed">horizontal speed in m/s</param>
        /// <param name="look">mouse delta this frame</param>
        public void Animate(float speed, bool grounded, Vector2 look, bool ads)
        {
            float dt = Time.deltaTime;
            adsBlend = Mathf.MoveTowards(adsBlend, ads ? 1f : 0f, dt * 7f);

            // Spring the recoil back to rest.
            kickVelZ += (-kickZ * 220f - kickVelZ * 24f) * dt;
            kickZ += kickVelZ * dt;
            kickVelPitch += (-kickPitch * 220f - kickVelPitch * 22f) * dt;
            kickPitch += kickVelPitch * dt;
            kick.localPosition = new Vector3(0, 0, kickZ * 0.05f);
            kick.localRotation = Quaternion.Euler(kickPitch * (1f - 0.6f * adsBlend), 0, 0);

            if (grounded && speed > 0.5f) bobPhase += dt * Mathf.Lerp(6f, 11f, speed / 7.5f);
            float bobAmt = Mathf.Clamp01(speed / 7.5f) * (1f - 0.85f * adsBlend);
            var bob = new Vector3(Mathf.Cos(bobPhase) * 0.012f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.014f, 0) * bobAmt;

            swayOffset = Vector2.Lerp(swayOffset, -look * 0.0009f, dt * 8f);
            swayOffset = Vector2.ClampMagnitude(swayOffset, 0.03f);

            var pos = Vector3.Lerp(HipPos, AdsPos[current], adsBlend) + bob + (Vector3)(swayOffset * (1f - 0.7f * adsBlend));
            var rot = Quaternion.Euler(-swayOffset.y * 120f, swayOffset.x * 120f, swayOffset.x * 60f);

            // Reload: dip and roll the gun.
            float rk = (Time.time - reloadStart) / reloadLength;
            if (rk >= 0f && rk <= 1f)
            {
                float w = Mathf.Sin(rk * Mathf.PI);
                pos += new Vector3(0, -0.08f, -0.02f) * w;
                rot *= Quaternion.Euler(18f * w, 0, -35f * w);
            }
            // Switch: rise up from below.
            float sk = (Time.time - switchStart) / 0.3f;
            if (sk >= 0f && sk < 1f)
            {
                float w = 1f - sk;
                pos += new Vector3(0, -0.2f, 0) * w * w;
                rot *= Quaternion.Euler(30f * w * w, 0, 0);
            }
            sway.localPosition = pos;
            sway.localRotation = rot;

            if (flash.gameObject.activeSelf && Time.time > flashUntil) flash.gameObject.SetActive(false);
        }
    }
}
