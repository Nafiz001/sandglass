using System.Diagnostics;
using System.Reflection;
using Sandglass.Game;
using UnityEditor;
using UnityEngine;

namespace Sandglass.EditorTools
{
    /// <summary>
    /// Advances play mode frame by frame in real time, for automated checks
    /// while the editor is in the background (Unity stops ticking play mode
    /// when the editor is unfocused). Optionally aims at the opponent and
    /// fires when it is in sight, to exercise the whole combat loop.
    /// </summary>
    public static class PlaymodeDriver
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        public static string Run(float seconds, bool fight = false, float minDistance = 0f, bool stopOnSight = false, bool ads = false)
        {
            if (!EditorApplication.isPlaying) return "not playing";
            EditorApplication.isPaused = true;
            Time.captureFramerate = 30;
            var sw = Stopwatch.StartNew();
            int frames = Mathf.CeilToInt(seconds * 30f);
            int sight = 0, fired = 0;
            var fYaw = typeof(LocalPlayer).GetField("yaw", Private);
            var fPitch = typeof(LocalPlayer).GetField("pitch", Private);
            var fire = typeof(LocalPlayer).GetMethod("TryFire", Private);
            for (int i = 0; i < frames; i++)
            {
                var lpGo = GameObject.Find("LocalPlayer");
                var opp = GameObject.Find("Opponent");
                if (fight && lpGo != null && opp != null)
                {
                    var lp = lpGo.GetComponent<LocalPlayer>();
                    var target = opp.transform.position + Vector3.up * 1.2f;
                    var d = target - lp.EyePos;
                    bool visible = lp.CanMove && lp.Alive && d.magnitude > minDistance &&
                                   !Physics.Linecast(lp.EyePos, target, ~(1 << 2), QueryTriggerInteraction.Ignore);
                    if (visible)
                    {
                        sight++;
                        fYaw.SetValue(lp, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                        fPitch.SetValue(lp, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
                        lp.Ads = ads;
                        if (sight > 10) { fire.Invoke(lp, null); fired++; }
                        if (stopOnSight && sight > 14)
                        {
                            EditorApplication.Step();
                            return $"stopped on sight after {i} frames, distance {d.magnitude:0.0} m";
                        }
                    }
                    else sight = 0;
                }
                EditorApplication.Step();
                while (sw.ElapsedMilliseconds < (i + 1) * 1000L / 30) { }
            }
            return $"ran {frames} frames, fired {fired}";
        }

        public static string Status()
        {
            var gc = Object.FindFirstObjectByType<GameClient>();
            if (gc == null) return "no client";
            var phase = typeof(GameClient).GetField("phase", Private).GetValue(gc);
            var score = (int[])typeof(GameClient).GetField("score", Private).GetValue(gc);
            var lpGo = GameObject.Find("LocalPlayer");
            string me = lpGo != null ? $" hp {lpGo.GetComponent<LocalPlayer>().Hp}" : "";
            return $"phase {phase} score {score[0]}-{score[1]}{me}";
        }
    }
}
