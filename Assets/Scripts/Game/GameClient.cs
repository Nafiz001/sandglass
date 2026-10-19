using System.Collections;
using Sandglass.Net;
using Sandglass.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace Sandglass.Game
{
    /// <summary>
    /// Owns the connection and the flow between menu, matchmaking and match,
    /// and routes server messages to the players and the HUD.
    /// </summary>
    public class GameClient : MonoBehaviour
    {
        const string DefaultServer = "wss://sandglass.130-210-37-18.sslip.io/ws";
        const string EditorServer = "ws://127.0.0.1:7350/ws";
        const float InterpDelayTicks = 3f; // render the opponent 100 ms in the past
        const float TickRate = 30f;

        public Hud Hud { get; private set; }
        MainMenu menu;
        readonly NetSocket sock = new NetSocket();

        enum Phase { Menu, Connecting, Waiting, Match, Ended }
        Phase phase = Phase.Menu;
        OutHello hello;
        string lastMode = "bot", lastRoom = "";

        Camera cam;
        LocalPlayer local;
        RemotePlayer remote;
        int you;
        MsgMatch match;
        string roundState = "";
        int round;
        readonly int[] score = new int[2];
        Vector3 spawnPos;
        float spawnYaw;
        int latestTick = -1;
        float latestTickAt;
        int lastCountdownBeep;
        float orbit;

        float EstTick => latestTick + (Time.time - latestTickAt) * TickRate;
        public int RenderTick => Mathf.Max(0, Mathf.FloorToInt(EstTick - InterpDelayTicks));

        string ServerUrl
        {
            get
            {
                var q = NetSocket.PageQuery("server");
                if (!string.IsNullOrEmpty(q)) return q;
                return Application.isEditor ? EditorServer : DefaultServer;
            }
        }

        string HttpBase => ServerUrl.Replace("wss://", "https://").Replace("ws://", "http://").Replace("/ws", "");

        void Start()
        {
            Application.runInBackground = true; // keep the socket and match running when unfocused
            LocalPlayer.Sensitivity = PlayerPrefs.GetFloat("sens", 1f);
            cam = Camera.main;
            Ui.EnsureEventSystem();
            Hud = new GameObject("Hud").AddComponent<Hud>();
            Hud.Build();
            Hud.OnLeave = LeaveToMenu;
            Hud.OnPlayAgain = () =>
            {
                string mode = lastMode;
                LeaveToMenu();
                Play(mode, ""); // a private rematch opens a fresh room
            };
            menu = new GameObject("MainMenu").AddComponent<MainMenu>();
            menu.Build();
            menu.OnPlay = Play;
            menu.OnCancel = LeaveToMenu;
            menu.Show();
            Sfx.StartWind();
            Fx.AmbientDust(cam.transform);
            StartCoroutine(PollStats());
        }

        IEnumerator PollStats()
        {
            while (true)
            {
                if (phase == Phase.Menu)
                {
                    using (var req = UnityWebRequest.Get(HttpBase + "/stats"))
                    {
                        req.timeout = 5;
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success)
                        {
                            var s = JsonUtility.FromJson<ServerStats>(req.downloadHandler.text);
                            menu.SetStatus("SERVER ONLINE   ·   " + s.online + (s.online == 1 ? " PLAYER" : " PLAYERS") + "   ·   " + s.matches + (s.matches == 1 ? " MATCH" : " MATCHES"));
                        }
                        else menu.SetStatus("SERVER UNREACHABLE");
                    }
                }
                yield return new WaitForSeconds(6f);
            }
        }

        [System.Serializable] class ServerStats { public int online, matches, waiting, rooms; }

        public void Send(string json)
        {
            if (sock.State == NetSocket.Status.Open) sock.Send(json);
        }

        void Play(string mode, string room)
        {
            lastMode = mode;
            lastRoom = room;
            hello = new OutHello { name = menu.PlayerName, mode = mode, room = room };
            sock.Connect(ServerUrl);
            phase = Phase.Connecting;
            menu.ShowWaiting("CONNECTING", "Reaching the match server", false);
        }

        void LeaveToMenu()
        {
            if (phase == Phase.Match) Send("{\"t\":\"leave\"}");
            sock.Close();
            phase = Phase.Menu;
            TearDownMatch();
            Hud.Hide();
            menu.Show();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void TearDownMatch()
        {
            cam.transform.SetParent(null, true);
            if (local != null) Destroy(local.gameObject);
            if (remote != null) Destroy(remote.gameObject);
            local = null;
            remote = null;
            latestTick = -1;
        }

        void Update()
        {
            while (sock.TryReceive(out var raw)) Handle(raw);

            if (phase == Phase.Connecting)
            {
                if (sock.State == NetSocket.Status.Open)
                {
                    Send(JsonUtility.ToJson(hello));
                    phase = Phase.Waiting;
                    menu.ShowWaiting("JOINING", "", false);
                }
                else if (sock.State == NetSocket.Status.Closed)
                {
                    phase = Phase.Menu;
                    menu.ShowError("Could not reach the match server. Try again in a moment.");
                }
            }
            else if ((phase == Phase.Waiting || phase == Phase.Match) && sock.State == NetSocket.Status.Closed)
            {
                LeaveToMenu();
                menu.ShowError("Lost connection to the match server.");
            }

            UpdateFocus();
            Fx.Tick();
            if (remote != null && latestTick >= 0) remote.Render(EstTick - InterpDelayTicks);
            Hud.Tick(local);
            if (phase != Phase.Match && phase != Phase.Ended) OrbitCamera();
        }

        void UpdateFocus()
        {
            if (phase != Phase.Match || local == null) return;
            var mouse = Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (!locked && mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                locked = true;
            }
            local.HasFocus = locked;
            Hud.SetPaused(!locked);
        }

        void OrbitCamera()
        {
            if (cam.transform.parent != null) return;
            orbit += Time.deltaTime * 2.2f;
            float a = orbit * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(a) * 31f, 8.5f + Mathf.Sin(a * 0.7f) * 1.5f, Mathf.Cos(a) * 31f);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0, 2.5f, 0) - pos);
            cam.fieldOfView = 55f;
        }

        void Handle(string raw)
        {
            var head = JsonUtility.FromJson<MsgHead>(raw);
            if (head == null) return;
            switch (head.t)
            {
                case "snap": OnSnap(JsonUtility.FromJson<MsgSnap>(raw)); break;
                case "shot": OnShot(JsonUtility.FromJson<MsgShot>(raw)); break;
                case "round": OnRound(JsonUtility.FromJson<MsgRound>(raw)); break;
                case "correct": OnCorrect(JsonUtility.FromJson<MsgCorrect>(raw)); break;
                case "match": OnMatch(JsonUtility.FromJson<MsgMatch>(raw)); break;
                case "end": OnEnd(JsonUtility.FromJson<MsgEnd>(raw)); break;
                case "queue": OnQueue(JsonUtility.FromJson<MsgQueue>(raw)); break;
                case "note": Hud.Notice(JsonUtility.FromJson<MsgError>(raw).msg, 6f); break;
                case "error":
                    sock.Close();
                    phase = Phase.Menu;
                    menu.ShowError(JsonUtility.FromJson<MsgError>(raw).msg);
                    break;
            }
        }

        void OnQueue(MsgQueue q)
        {
            if (q.mode == "private")
                menu.ShowWaiting("ROOM  " + q.room, "Share this code with your opponent", false);
            else
                menu.ShowWaiting("FINDING OPPONENT", "", true);
        }

        void OnMatch(MsgMatch m)
        {
            TearDownMatch();
            match = m;
            you = m.you;
            score[0] = score[1] = 0;
            round = 0;
            phase = Phase.Match;
            menu.Hide();

            var go = new GameObject("LocalPlayer") { layer = 2 }; // Ignore Raycast
            go.AddComponent<CharacterController>();
            local = go.AddComponent<LocalPlayer>();
            go.transform.position = spawnPos;
            local.Setup(this, cam);

            remote = new GameObject("Opponent").AddComponent<RemotePlayer>();
            remote.Build(Models.Accent);
            remote.ResetNext();

            string opp = m.names[1 - you] + (m.bots[1 - you] ? "  [BOT]" : "");
            Hud.Show(m.names[you], opp);
            Hud.Banner("", m.room != "" && m.room != null ? "ROOM " + m.room : "", Ui.Ink, 3f);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnCorrect(MsgCorrect c)
        {
            spawnPos = Proto.V(c.p);
            spawnYaw = c.yaw;
            if (local != null) local.Teleport(spawnPos, roundState == "live" ? local.Yaw : spawnYaw);
        }

        void OnRound(MsgRound r)
        {
            roundState = r.state;
            round = r.round;
            score[0] = r.score[0];
            score[1] = r.score[1];
            Hud.SetScore(score[you], score[1 - you], round);
            if (local == null) return;
            switch (r.state)
            {
                case "countdown":
                    local.ResetRound(spawnPos, spawnYaw);
                    local.Revive();
                    local.CanMove = false;
                    remote.ResetNext();
                    bool matchPoint = score[0] == 2 || score[1] == 2;
                    Hud.Banner("ROUND " + round, matchPoint ? "MATCH POINT" : "FIRST TO THREE", Ui.Ink, 0f);
                    lastCountdownBeep = 4;
                    break;
                case "live":
                    local.CanMove = true;
                    Hud.Banner("FIGHT", "", Ui.Amber, 1.1f);
                    Sfx.Play(Sfx.Beep(true), 0.8f, null, 0f);
                    break;
                case "over":
                    local.CanMove = false;
                    bool won = r.winner == you;
                    string why = r.reason == "time" ? "TIME RAN OUT" : "";
                    if (r.winner < 0) Hud.Banner("DRAW", "TIME RAN OUT WITH EQUAL HEALTH", Ui.Ink, 3.5f);
                    else Hud.Banner(won ? "ROUND WON" : "ROUND LOST", why, won ? Ui.Amber : Ui.Red, 3.5f);
                    break;
            }
        }

        void OnEnd(MsgEnd e)
        {
            if (local == null) return;
            phase = Phase.Ended;
            bool won = e.winner == you;
            string reason = e.reason == "opponent left" ? (won ? "Your opponent left the match." : "") : "";
            Hud.End(won, e.score[you], e.score[1 - you], reason);
            Sfx.Play(Sfx.Sting(won), 0.8f, null, 0f);
            local.CanMove = false;
            local.HasFocus = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnSnap(MsgSnap s)
        {
            if (local == null || s.pl == null || s.pl.Length < 2) return;
            if (s.tick > latestTick)
            {
                latestTick = s.tick;
                latestTickAt = Time.time;
            }
            local.OnSnapshot(s.pl[you], s.mag, s.res);
            remote.Push(s.tick, s.pl[1 - you]);
            if (s.st == "countdown")
            {
                Hud.SetClock(100f);
                int n = Mathf.CeilToInt(s.left);
                if (n < lastCountdownBeep && n > 0)
                {
                    lastCountdownBeep = n;
                    Sfx.Play(Sfx.Beep(false), 0.6f, null, 0f);
                    Hud.Banner("ROUND " + round, n.ToString(), Ui.Ink, 0f);
                }
            }
            else if (s.st == "live") Hud.SetClock(s.left);
        }

        void OnShot(MsgShot s)
        {
            if (local == null) return;
            if (s.by == you)
            {
                if (s.hit >= 0)
                {
                    Hud.HitMarker(s.head, s.dmg);
                    Sfx.Play(s.head ? Sfx.Headshot() : Sfx.HitMarker(), 0.9f, null, 0f);
                }
                return;
            }
            var from = remote != null ? remote.MuzzlePos : Proto.V(s.o);
            var end = Proto.V(s.e);
            Fx.Tracer(from, end, new Color(1.2f, 3.2f, 4f), 0.035f);
            Sfx.Play(s.w == 0 ? Sfx.Rifle() : Sfx.Pistol(), 0.9f, from);
            var dir = (end - Proto.V(s.o)).normalized;
            if (s.hit < 0) Fx.Impact(end, -dir);
        }

        void OnApplicationQuit() => sock.Close();
    }
}
