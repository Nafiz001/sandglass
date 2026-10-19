using Sandglass.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandglass.Game
{
    /// <summary>
    /// The player's own soldier: movement is simulated here and sent to the
    /// server, which checks it; shots are drawn at once and sent for the
    /// server to resolve; ammo is predicted and corrected from snapshots.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class LocalPlayer : MonoBehaviour
    {
        public const float EyeHeight = 1.6f;
        const float WalkSpeed = 5f, SprintSpeed = 7.2f, AdsSpeed = 3.2f;
        const float Gravity = 20f, JumpHeight = 1.1f;
        const float HipFov = 72f, AdsFov = 50f;

        public GameClient Client;
        public Camera Cam;
        public WeaponView View;
        public static float Sensitivity = 1f;

        CharacterController cc;
        Transform pitchPivot;
        float yaw, pitch;
        Vector3 horizontal;
        float vertical;

        public bool Alive = true;
        public bool CanMove;   // round is live
        public bool HasFocus;  // not paused, pointer locked
        public int Hp = 100;
        public int Weapon;
        public readonly int[] Mag = new int[2];
        public readonly int[] Reserve = new int[2];
        public bool Reloading;
        public float Spread; // current cone in degrees, for the crosshair
        public bool Ads;

        float nextFire, lastShotAt = -10f, reloadUntil;
        float bloom;
        float sendTimer;
        int seq;
        readonly OutMove move = new OutMove();
        readonly OutShoot shot = new OutShoot();
        int worldMask;
        float deathTilt;

        public float Yaw => yaw;
        public float Pitch => pitch;
        public Vector3 EyePos => transform.position + Vector3.up * EyeHeight;

        public void Setup(GameClient client, Camera cam)
        {
            Client = client;
            cc = GetComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            pitchPivot = new GameObject("Eye").transform;
            pitchPivot.SetParent(transform, false);
            pitchPivot.localPosition = new Vector3(0, EyeHeight, 0);
            Cam = cam;
            Cam.transform.SetParent(pitchPivot, false);
            Cam.transform.localPosition = Vector3.zero;
            Cam.transform.localRotation = Quaternion.identity;
            Cam.fieldOfView = HipFov;
            View = new GameObject("WeaponView").AddComponent<WeaponView>();
            View.transform.SetParent(Cam.transform, false);
            View.Build();
            worldMask = ~(1 << gameObject.layer);
            ResetRound(transform.position, transform.eulerAngles.y);
        }

        public void ResetRound(Vector3 pos, float yawDeg)
        {
            Teleport(pos, yawDeg);
            pitch = 0;
            Alive = true;
            Hp = 100;
            Weapon = 0;
            for (int i = 0; i < 2; i++) { Mag[i] = Weapons.All[i].Mag; Reserve[i] = Weapons.All[i].Reserve; }
            Reloading = false;
            reloadUntil = 0;
            bloom = 0;
            deathTilt = 0;
            View.Show(0);
            View.CancelReload();
        }

        public void Teleport(Vector3 pos, float yawDeg)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            yaw = yawDeg;
            horizontal = Vector3.zero;
            vertical = 0;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        /// <summary>Applies the authoritative state for this player.</summary>
        public void OnSnapshot(SnapPlayer me, int mag, int res)
        {
            if (me.hp < Hp && me.alive) Client.Hud.Damaged(Hp - me.hp);
            Hp = me.hp;
            if (Alive && !me.alive) Die();
            Alive = me.alive;
            bool predicting = Time.time - lastShotAt < 0.35f || Time.time < reloadUntil;
            if (!predicting && me.w == Weapon)
            {
                Mag[Weapon] = mag;
                Reserve[Weapon] = res;
            }
            Reloading = me.rl || Time.time < reloadUntil;
        }

        void Die()
        {
            Sfx.Play(Sfx.Hurt(), 1f);
            View.gameObject.SetActive(false);
        }

        public void Revive() => View.gameObject.SetActive(true);

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            float dt = Time.deltaTime;

            Vector2 look = HasFocus ? mouse.delta.ReadValue() : Vector2.zero;
            if (Alive)
            {
                float s = 0.09f * Sensitivity * (Ads ? 0.65f : 1f);
                yaw += look.x * s;
                pitch = Mathf.Clamp(pitch - look.y * s, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (!Alive) deathTilt = Mathf.MoveTowards(deathTilt, 1f, dt * 2.5f);
            pitchPivot.localPosition = new Vector3(0, Mathf.Lerp(EyeHeight, 0.35f, deathTilt), 0);
            pitchPivot.localRotation = Quaternion.Euler(pitch, 0, Mathf.Lerp(0, 70f, deathTilt));

            bool controls = HasFocus && Alive;
            Ads = controls && mouse.rightButton.isPressed && !Reloading;
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, Ads ? AdsFov : HipFov, dt * 12f);

            // Movement
            Vector2 input = Vector2.zero;
            if (controls && CanMove)
            {
                if (kb.wKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed) input.y -= 1;
                if (kb.dKey.isPressed) input.x += 1;
                if (kb.aKey.isPressed) input.x -= 1;
            }
            bool sprint = kb.leftShiftKey.isPressed && input.y > 0 && !Ads;
            float speed = Ads ? AdsSpeed : sprint ? SprintSpeed : WalkSpeed;
            var wish = (transform.forward * input.y + transform.right * input.x);
            wish = (wish.sqrMagnitude > 1f ? wish.normalized : wish) * speed;
            bool grounded = cc.isGrounded;
            horizontal = Vector3.MoveTowards(horizontal, wish, (grounded ? 55f : 12f) * dt);
            if (grounded && vertical < 0) vertical = -2f;
            if (controls && CanMove && grounded && kb.spaceKey.wasPressedThisFrame)
                vertical = Mathf.Sqrt(2f * Gravity * JumpHeight);
            vertical -= Gravity * dt;
            if (Alive) cc.Move((horizontal + Vector3.up * vertical) * dt);

            // Weapons
            if (controls && CanMove)
            {
                if (kb.digit1Key.wasPressedThisFrame) SwitchTo(0);
                if (kb.digit2Key.wasPressedThisFrame) SwitchTo(1);
                if (kb.qKey.wasPressedThisFrame) SwitchTo(1 - Weapon);
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.1f) SwitchTo(1 - Weapon);
                if (kb.rKey.wasPressedThisFrame) StartReload();
                var w = Weapons.All[Weapon];
                bool trigger = w.Auto ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
                if (trigger) TryFire();
            }
            if (reloadUntil > 0 && Time.time >= reloadUntil) reloadUntil = 0;

            bloom = Mathf.MoveTowards(bloom, 0f, dt * 3.5f);
            var wi = Weapons.All[Weapon];
            float moving = Mathf.Clamp01(horizontal.magnitude / SprintSpeed);
            Spread = (Ads ? wi.AdsSpread : wi.HipSpread) + bloom + moving * (Ads ? 0.6f : 2.2f) + (grounded ? 0f : 2.5f);

            View.Animate(horizontal.magnitude, grounded, look, Ads);

            // Report position to the server at ~30 Hz.
            sendTimer -= dt;
            if (sendTimer <= 0f && Alive && CanMove)
            {
                sendTimer = 1f / 30f;
                move.seq = ++seq;
                Proto.Fill(move.p, transform.position);
                move.yaw = yaw;
                move.pitch = pitch;
                Client.Send(JsonUtility.ToJson(move));
            }
        }

        void SwitchTo(int w)
        {
            if (w == Weapon) return;
            Weapon = w;
            reloadUntil = 0;
            Reloading = false;
            nextFire = Mathf.Max(nextFire, Time.time + 0.3f);
            View.Show(w);
            View.CancelReload();
            Client.Send("{\"t\":\"switch\",\"w\":" + w + "}");
        }

        void StartReload()
        {
            var w = Weapons.All[Weapon];
            if (Reloading || Mag[Weapon] >= w.Mag || Reserve[Weapon] <= 0) return;
            Reloading = true;
            reloadUntil = Time.time + w.Reload;
            View.Reload(w.Reload);
            Sfx.Play(Sfx.Reload(), 0.8f);
            Client.Send("{\"t\":\"reload\"}");
            // Predict the refill so the HUD is right when the animation ends.
            int n = Mathf.Min(w.Mag - Mag[Weapon], Reserve[Weapon]);
            int weapon = Weapon;
            pendingRefill = n;
            pendingWeapon = weapon;
        }

        int pendingRefill, pendingWeapon;

        void LateUpdate()
        {
            if (pendingRefill > 0 && reloadUntil == 0f && Weapon == pendingWeapon)
            {
                Mag[pendingWeapon] += pendingRefill;
                Reserve[pendingWeapon] -= pendingRefill;
                pendingRefill = 0;
                Reloading = false;
            }
            else if (Weapon != pendingWeapon) pendingRefill = 0;
        }

        void TryFire()
        {
            var w = Weapons.All[Weapon];
            if (Time.time < nextFire || Reloading) return;
            if (Mag[Weapon] <= 0)
            {
                nextFire = Time.time + 0.25f;
                Sfx.Play(Sfx.Click(), 0.7f);
                StartReload();
                return;
            }
            nextFire = Time.time + w.Interval;
            lastShotAt = Time.time;
            Mag[Weapon]--;

            var origin = Cam.transform.position;
            var dir = Cone(Cam.transform.forward, Spread);
            bloom = Mathf.Min(bloom + w.Bloom, 3f);

            Vector3 end;
            if (Physics.Raycast(origin, dir, out var hit, 200f, worldMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                Fx.Impact(hit.point, hit.normal);
            }
            else end = origin + dir * 200f;
            Fx.Tracer(View.Muzzle.position, end, new Color(4f, 2.6f, 1.2f), 0.025f);
            View.Fire(w.Kick);
            Sfx.Play(Weapon == 0 ? Sfx.Rifle() : Sfx.Pistol(), Weapon == 0 ? 0.55f : 0.6f);

            float up = w.RecoilUp * (Ads ? 0.6f : 1f);
            pitch -= up;
            yaw += Random.Range(-w.RecoilSide, w.RecoilSide);

            Proto.Fill(shot.o, origin);
            Proto.Fill(shot.d, dir);
            shot.w = Weapon;
            shot.tick = Client.RenderTick;
            Client.Send(JsonUtility.ToJson(shot));
        }

        static Vector3 Cone(Vector3 forward, float degrees)
        {
            if (degrees <= 0.001f) return forward;
            var r = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right == Vector3.zero) right = Vector3.right;
            var up = Vector3.Cross(forward, right);
            return (forward + right * r.x + up * r.y).normalized;
        }
    }
}
