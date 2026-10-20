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
        CharacterView view;
        bool alive = true;
        int hp = 100;
        Vector3 lastPos;
        float yawShown, pitchShown, speed;
        bool pendingSnap;

        public Vector3 MuzzlePos => view.Muzzle;
        public bool Alive => alive;

        /// <param name="bot">bots wear the kit's enemy character, people its soldier</param>
        public void Build(bool bot)
        {
            view = new CharacterView(transform, bot);
        }

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
            view.SetWeapon(s.w);
            if (s.alive && s.hp < hp) view.Hit();
            if (alive && !s.alive) view.Die();
            hp = s.hp;
            alive = s.alive;
        }

        void Snap(Vector3 pos, float yaw)
        {
            buffer.Clear();
            transform.position = lastPos = pos;
            yawShown = yaw;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            alive = true;
            hp = 100;
            view.Revive();
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
            pitchShown = Mathf.Lerp(a.pitch, b.pitch, t);

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var vel = (pos - lastPos) / dt;
            lastPos = pos;
            transform.position = pos;
            yawShown = Mathf.LerpAngle(yawShown, yaw, dt * 20f);
            transform.rotation = Quaternion.Euler(0, yawShown, 0);
            speed = Mathf.Lerp(speed, new Vector3(vel.x, 0, vel.z).magnitude, dt * 10f);
            view.Animate(speed, alive);
        }

        // After the Animation component has posed the skeleton this frame.
        void LateUpdate()
        {
            if (alive) view.AimPitch(pitchShown);
        }
    }
}
