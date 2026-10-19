using UnityEngine;
using UnityEngine.UI;

namespace Sandglass.UI
{
    /// <summary>The title screen, matchmaking status and errors.</summary>
    public class MainMenu : MonoBehaviour
    {
        Canvas canvas;
        GameObject buttons, waiting;
        InputField nameField, codeField;
        Text waitTitle, waitSub, status, error;
        float waitStart;
        bool counting;

        public System.Action<string, string> OnPlay; // mode, room
        public System.Action OnCancel;

        public string PlayerName => nameField.text;

        public void Build()
        {
            canvas = Ui.Canvas("Menu", 20);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            // Warm gradient wash behind the left column for legibility.
            var wash = Ui.Rect("Wash", root, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(1100, 2400));
            var washImg = Ui.Box(wash, new Color(0.05f, 0.03f, 0.02f, 0.62f));
            washImg.sprite = HorizontalFade();

            var left = new Vector2(0, 1);
            Ui.Label(root, "SANDGLASS", 150, true, TextAnchor.UpperLeft, Ui.Ink, left, left, new Vector2(120, -110), new Vector2(1000, 170))
                .gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
            Ui.Label(root, "1V1 ARENA SHOOTER  ·  FIRST TO THREE ROUNDS", 28, true, TextAnchor.UpperLeft, Ui.Amber, left, left, new Vector2(126, -270), new Vector2(1000, 40));

            Ui.Label(root, "CALLSIGN", 20, false, TextAnchor.UpperLeft, Ui.Dim, left, left, new Vector2(126, -350), new Vector2(400, 30));
            nameField = Ui.Input(root, "Your name", 16, left, left, new Vector2(120, -380), new Vector2(460, 62));
            nameField.text = PlayerPrefs.GetString("name", "");

            buttons = Ui.Rect("Buttons", root, left, left, new Vector2(120, -470), new Vector2(460, 420)).gameObject;
            var b = buttons.transform;
            var tl = new Vector2(0, 1);
            Ui.Button(b, "QUICK MATCH", () => Play("quick", ""), true, tl, tl, new Vector2(0, 0), new Vector2(460, 72));
            Ui.Button(b, "PRACTICE VS BOT", () => Play("bot", ""), false, tl, tl, new Vector2(0, -88), new Vector2(460, 64));
            Ui.Button(b, "CREATE PRIVATE ROOM", () => Play("private", ""), false, tl, tl, new Vector2(0, -168), new Vector2(460, 64));
            codeField = Ui.Input(b, "Room code", 4, tl, tl, new Vector2(0, -248), new Vector2(250, 64));
            Ui.Button(b, "JOIN ROOM", () => Play("private", codeField.text.Trim().ToUpperInvariant()), false, tl, tl, new Vector2(266, -248), new Vector2(194, 64));
            Ui.Label(b, "MOUSE SENSITIVITY", 18, false, TextAnchor.UpperLeft, Ui.Dim, tl, tl, new Vector2(4, -340), new Vector2(300, 26));
            Ui.Slider(b, 0.2f, 3f, Game.LocalPlayer.Sensitivity, v => { Game.LocalPlayer.Sensitivity = v; PlayerPrefs.SetFloat("sens", v); },
                tl, tl, new Vector2(0, -372), new Vector2(460, 30));

            waiting = Ui.Rect("Waiting", root, left, left, new Vector2(120, -470), new Vector2(700, 300)).gameObject;
            var w = waiting.transform;
            waitTitle = Ui.Label(w, "FINDING OPPONENT", 54, true, TextAnchor.UpperLeft, Ui.Ink, tl, tl, Vector2.zero, new Vector2(900, 70));
            waitSub = Ui.Label(w, "", 28, false, TextAnchor.UpperLeft, Ui.Amber, tl, tl, new Vector2(4, -76), new Vector2(900, 40));
            Ui.Button(w, "CANCEL", () => OnCancel?.Invoke(), false, tl, tl, new Vector2(0, -150), new Vector2(240, 64));
            waiting.SetActive(false);

            error = Ui.Label(root, "", 26, false, TextAnchor.UpperLeft, Ui.Red, left, left, new Vector2(126, -920), new Vector2(900, 40));

            var bottomLeft = new Vector2(0, 0);
            Ui.Label(root, "WASD MOVE   SHIFT SPRINT   SPACE JUMP   RMB AIM   R RELOAD   1 / 2 / Q SWITCH   ESC PAUSE",
                18, false, TextAnchor.LowerLeft, Ui.Dim, bottomLeft, bottomLeft, new Vector2(126, 40), new Vector2(1400, 30));
            status = Ui.Label(root, "", 20, false, TextAnchor.LowerRight, Ui.Dim, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50, 40), new Vector2(900, 30));
            Ui.Label(root, "UNITY  ·  GO  ·  SERVER-AUTHORITATIVE", 18, false, TextAnchor.UpperRight, Ui.Dim, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -40), new Vector2(900, 30));
        }

        static Sprite fade;

        static Sprite HorizontalFade()
        {
            if (fade != null) return fade;
            var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++) tex.SetPixel(x, 0, new Color(1, 1, 1, Mathf.Clamp01(1.3f - x / 50f)));
            tex.Apply();
            fade = Sprite.Create(tex, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f));
            return fade;
        }

        void Play(string mode, string room)
        {
            if (mode == "private" && room.Length > 0 && room.Length != 4)
            {
                ShowError("Room codes are four characters.");
                return;
            }
            PlayerPrefs.SetString("name", nameField.text);
            OnPlay?.Invoke(mode, room);
        }

        public void Show()
        {
            canvas.gameObject.SetActive(true);
            buttons.SetActive(true);
            waiting.SetActive(false);
            counting = false;
        }

        public void Hide() => canvas.gameObject.SetActive(false);

        public void ShowWaiting(string title, string sub, bool count)
        {
            canvas.gameObject.SetActive(true);
            buttons.SetActive(false);
            waiting.SetActive(true);
            waitTitle.text = title;
            waitSub.text = sub;
            error.text = "";
            counting = count;
            waitStart = Time.time;
        }

        public void ShowError(string msg)
        {
            Show();
            error.text = msg;
        }

        public void SetStatus(string s) => status.text = s;

        void Update()
        {
            if (counting && waiting.activeSelf)
                waitSub.text = "Searching  " + Ui.Clock(Time.time - waitStart) + "   ·   a bot steps in if nobody shows up";
        }
    }
}
