using System;
using UnityEngine;

namespace Sandglass.Net
{
    // Mirrors server/internal/game/protocol.go. JsonUtility needs concrete
    // classes, so each message is read twice: once for "t", once in full.

    [Serializable] public class MsgHead { public string t; }

    [Serializable] public class MsgQueue { public string t, mode, room; }

    [Serializable]
    public class MsgMatch
    {
        public string t, room, note;
        public int you;
        public string[] names;
        public bool[] bots;
    }

    [Serializable]
    public class MsgRound
    {
        public string t, state, reason;
        public int round, winner;
        public int[] score;
    }

    [Serializable]
    public class MsgEnd
    {
        public string t, reason;
        public int winner;
        public int[] score;
    }

    [Serializable] public class MsgCorrect { public string t; public float[] p; public float yaw; }

    [Serializable]
    public class MsgShot
    {
        public string t;
        public int by, w, hit, dmg;
        public float[] o, e;
        public bool head;
    }

    [Serializable]
    public class SnapPlayer
    {
        public float[] p;
        public float yaw, pitch;
        public int hp, w;
        public bool alive, rl;
    }

    [Serializable]
    public class MsgSnap
    {
        public string t, st;
        public int tick;
        public float left;
        public SnapPlayer[] pl;
        public int mag, res;
    }

    [Serializable] public class MsgError { public string t, msg; }

    // Outgoing

    [Serializable] public class OutHello { public string t = "hello", name, mode, room; }

    [Serializable]
    public class OutMove
    {
        public string t = "move";
        public int seq;
        public float[] p = new float[3];
        public float yaw, pitch;
    }

    [Serializable]
    public class OutShoot
    {
        public string t = "shoot";
        public float[] o = new float[3], d = new float[3];
        public int w, tick;
    }

    [Serializable] public class OutWeapon { public string t; public int w; }

    public static class Proto
    {
        public static Vector3 V(float[] a) => a != null && a.Length == 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;

        public static void Fill(float[] dst, Vector3 v) { dst[0] = v.x; dst[1] = v.y; dst[2] = v.z; }
    }
}
