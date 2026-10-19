using System.Collections.Generic;
using Sandglass.Net;
using UnityEngine;

namespace Sandglass.Game
{
    /// <summary>
    /// The opponent as seen locally: snapshots are buffered and played back
    /// slightly in the past so motion is smooth between 30 Hz updates.
    /// </summary>
    public class RemotePlayer : MonoBehaviour
    {
        struct Sample
        {
            public float tick;
            public Vector3 pos;
            public float yaw, pitch;
        }

        readonly List<Sample> buffer = new List<Sample>();
        AvatarRig rig;
        int shownWeapon;
        bool alive = true;
        float deathT;
        float walkPhase;
        Vector3 lastPos;
        float yawShown;

        public Vector3 MuzzlePos => rig.Muzzle.position;
        public bool Alive => alive;

        public void Build(Color accent)
        {
            rig = Models.Soldier(transform, accent);
        }

        bool pendingSnap;

        /// <summary>The next snapshot teleports instead of interpolating (new round).</summary>
        public void ResetNext() => pendingSnap = true;

        public void Push(int tick, SnapPlayer s)
        {
            if (pendingSnap)
            {
                pendingSnap = false;
                Snap(Proto.V(s.p), s.yaw);
            }
            if (buffer.Count > 0 && tick <= buffer[buffer.Count - 1].tick) return;
            buffer.Add(new Sample { tick = tick, pos = Proto.V(s.p), yaw = s.yaw, pitch = s.pitch });
            if (buffer.Count > 40) buffer.RemoveAt(0);
            if (s.w != shownWeapon) ShowWeapon(s.w);
            if (alive && !s.alive) deathT = 0f;
            alive = s.alive;
        }

        public void Snap(Vector3 pos, float yaw)
        {
            buffer.Clear();
            transform.position = lastPos = pos;
            yawShown = yaw;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            alive = true;
            rig.Root.localRotation = Quaternion.identity;
            rig.Root.localPosition = Vector3.zero;
        }

        void ShowWeapon(int w)
        {
            shownWeapon = w;
            for (int i = rig.Gun.childCount - 1; i >= 0; i--) Destroy(rig.Gun.GetChild(i).gameObject);
            rig.Muzzle = Models.Gun(rig.Gun, w, false);
        }

        /// <param name="renderTick">fractional server tick to display</param>
        public void Render(float renderTick)
        {
            if (buffer.Count == 0) return;
            Sample a = buffer[0], b = buffer[0];
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].tick <= renderTick) a = buffer[i];
                if (buffer[i].tick >= renderTick) { b = buffer[i]; break; }
                b = buffer[i];
            }
            float t = b.tick > a.tick ? Mathf.Clamp01((renderTick - a.tick) / (b.tick - a.tick)) : 1f;
            var pos = Vector3.Lerp(a.pos, b.pos, t);
            float yaw = Mathf.LerpAngle(a.yaw, b.yaw, t);
            float pitch = Mathf.Lerp(a.pitch, b.pitch, t);

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var vel = (pos - lastPos) / dt;
            lastPos = pos;
            transform.position = pos;
            yawShown = Mathf.LerpAngle(yawShown, yaw, dt * 20f);
            transform.rotation = Quaternion.Euler(0, yawShown, 0);

            float speed = new Vector3(vel.x, 0, vel.z).magnitude;
            walkPhase += dt * Mathf.Lerp(0f, 10f, Mathf.Clamp01(speed / 6f));
            float swing = Mathf.Sin(walkPhase) * 32f * Mathf.Clamp01(speed / 4f);
            rig.LegL.localRotation = Quaternion.Euler(swing, 0, 0);
            rig.LegR.localRotation = Quaternion.Euler(-swing, 0, 0);
            rig.Torso.localRotation = Quaternion.Euler(pitch * 0.45f, 0, Mathf.Sin(walkPhase * 0.5f) * 2f * Mathf.Clamp01(speed / 4f));
            rig.Head.localRotation = Quaternion.Euler(pitch * 0.45f, 0, 0);

            if (!alive)
            {
                deathT = Mathf.Min(deathT + dt * 2.2f, 1f);
                float e = 1f - (1f - deathT) * (1f - deathT);
                rig.Root.localRotation = Quaternion.Euler(-85f * e, 0, 12f * e);
                rig.Root.localPosition = new Vector3(0, 0.15f * e, -0.2f * e);
            }
        }
    }
}
