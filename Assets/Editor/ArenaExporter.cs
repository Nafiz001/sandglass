using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Sandglass.EditorTools
{
    /// <summary>
    /// Writes the arena's colliders and spawn points to the Go server's
    /// embedded arena.json, so server-side hit detection, line of sight and
    /// bot navigation use exactly the geometry players see.
    /// </summary>
    public static class ArenaExporter
    {
        const string OutputPath = "server/internal/arena/arena.json";

        // Thin decorative cloth that bullets should pass through.
        static readonly string[] SoftNames = { "Cloth", "Awning", "Banner" };

        [MenuItem("Sandglass/Export Arena For Server")]
        public static void Export()
        {
            var root = GameObject.Find("Arena");
            if (root == null)
            {
                Debug.LogError("[ArenaExporter] No 'Arena' object in the open scene.");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("{\n\"boxes\":[");
            int boxes = 0, capsules = 0;
            bool first = true;
            foreach (var bc in root.GetComponentsInChildren<BoxCollider>())
            {
                if (IsSoft(bc.name) || !InPlayArea(bc.bounds)) continue;
                var t = bc.transform;
                var half = Vector3.Scale(bc.size, t.lossyScale) * 0.5f;
                half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
                sb.Append(first ? "\n" : ",\n");
                first = false;
                sb.Append("{\"c\":").Append(Vec(t.TransformPoint(bc.center)))
                  .Append(",\"h\":").Append(Vec(half))
                  .Append(",\"q\":").Append(Quat(t.rotation)).Append('}');
                boxes++;
            }
            sb.Append("\n],\n\"capsules\":[");
            first = true;
            foreach (var cc in root.GetComponentsInChildren<CapsuleCollider>())
            {
                if (IsSoft(cc.name) || !InPlayArea(cc.bounds)) continue;
                var t = cc.transform;
                var s = t.lossyScale;
                Vector3 axis;
                float radiusScale, heightScale;
                switch (cc.direction)
                {
                    case 0: axis = t.right; heightScale = s.x; radiusScale = Mathf.Max(s.y, s.z); break;
                    case 2: axis = t.forward; heightScale = s.z; radiusScale = Mathf.Max(s.x, s.y); break;
                    default: axis = t.up; heightScale = s.y; radiusScale = Mathf.Max(s.x, s.z); break;
                }
                float r = cc.radius * Mathf.Abs(radiusScale);
                float halfLen = Mathf.Max(cc.height * Mathf.Abs(heightScale) * 0.5f - r, 0f);
                var c = t.TransformPoint(cc.center);
                sb.Append(first ? "\n" : ",\n");
                first = false;
                sb.Append("{\"a\":").Append(Vec(c - axis * halfLen))
                  .Append(",\"b\":").Append(Vec(c + axis * halfLen))
                  .Append(",\"r\":").Append(F(r)).Append('}');
                capsules++;
            }
            sb.Append("\n],\n\"spawns\":[");
            first = true;
            var spawns = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name == "Spawn") spawns.Add(t);
            spawns.Sort((a, b) => a.position.z.CompareTo(b.position.z));
            foreach (var t in spawns)
            {
                sb.Append(first ? "\n" : ",\n");
                first = false;
                sb.Append("{\"p\":").Append(Vec(t.position)).Append(",\"yaw\":").Append(F(t.eulerAngles.y)).Append('}');
            }
            sb.Append("\n],\n\"bounds\":33.5\n}\n");

            var path = Path.Combine(Path.GetDirectoryName(Application.dataPath), OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[ArenaExporter] Wrote {boxes} boxes, {capsules} capsules, {spawns.Count} spawns to {OutputPath}");
        }

        static bool IsSoft(string name)
        {
            foreach (var s in SoftNames) if (name.Contains(s)) return true;
            return false;
        }

        // Dunes outside the outer wall never matter for gameplay.
        static bool InPlayArea(Bounds b) =>
            Mathf.Abs(b.center.x) < 40f && Mathf.Abs(b.center.z) < 40f;

        static string F(float f) => f.ToString("0.####", CultureInfo.InvariantCulture);
        static string Vec(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";
        static string Quat(Quaternion q) => $"[{F(q.x)},{F(q.y)},{F(q.z)},{F(q.w)}]";
    }
}
