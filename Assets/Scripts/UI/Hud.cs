using Sandglass.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Sandglass.UI
{
    /// <summary>The in-match overlay.</summary>
    public class Hud : MonoBehaviour
    {
        Canvas canvas;
        RectTransform cross;
        readonly RectTransform[] crossBars = new RectTransform[4];
        Image crossDot;
        RectTransform hitMarker;
        readonly Image[] hitBars = new Image[4];
        float hitUntil;
        Text dmgText;
        float dmgUntil;

        Text nameYou, nameOpp, scoreYou, scoreOpp, roundText, clock;
        Text bigText, subText;
        float bigUntil = -1f;
        Text hpText, ammoText, reserveText, weaponText, reloadText;
        Image hpFill;
        Image vignette;
        float hurt;
        GameObject pausePanel, endPanel;
        Text endTitle, endScore, endReason;
        Text noticeText;
        float noticeUntil;

        public System.Action OnLeave, OnPlayAgain;

        public void Build()
        {
            canvas = Ui.Canvas("HUD", 10);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;
            var center = new Vector2(0.5f, 0.5f);

            vignette = Ui.Fill("Vignette", root, new Color(0.8f, 0.05f, 0.02f, 0f), center, center, Vector2.zero, Vector2.zero);
            vignette.rectTransform.anchorMin = Vector2.zero;
            vignette.rectTransform.anchorMax = Vector2.one;
            vignette.rectTransform.sizeDelta = Vector2.zero;
            vignette.sprite = Ui.Vignette();

            // Crosshair
            cross = Ui.Rect("Crosshair", root, center, center, Vector2.zero, new Vector2(100, 100));
            for (int i = 0; i < 4; i++)
            {
                bool vertical = i < 2;
                var bar = Ui.Fill("Bar", cross, Ui.Ink, center, center, Vector2.zero, vertical ? new Vector2(3, 12) : new Vector2(12, 3));
                bar.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
                crossBars[i] = bar.rectTransform;
            }
            crossDot = Ui.Fill("Dot", cross, Ui.Ink, center, center, Vector2.zero, new Vector2(3, 3));

            hitMarker = Ui.Rect("HitMarker", root, center, center, Vector2.zero, new Vector2(60, 60));
            for (int i = 0; i < 4; i++)
            {
                var bar = Ui.Fill("Hit", hitMarker, Ui.Ink, center, center, Vector2.zero, new Vector2(3, 14));
                float a = 45f + 90f * i;
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, a);
                bar.rectTransform.anchoredPosition = Quaternion.Euler(0, 0, a) * new Vector2(0, 14);
                hitBars[i] = bar;
            }
            hitMarker.gameObject.SetActive(false);
            dmgText = Ui.Label(root, "", 30, true, TextAnchor.MiddleLeft, Ui.Ink, center, new Vector2(0, 0.5f), new Vector2(34, 26), new Vector2(200, 40));

            // Score bar
            var top = new Vector2(0.5f, 1f);
            var bar2 = Ui.Fill("ScoreBar", root, Ui.Panel, top, top, new Vector2(0, -24), new Vector2(560, 70));
            nameYou = Ui.Label(bar2.transform, "YOU", 24, true, TextAnchor.MiddleRight, Ui.Cyan, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(170, 40));
            scoreYou = Ui.Label(bar2.transform, "0", 52, true, TextAnchor.MiddleCenter, Ui.Ink, center, center, new Vector2(-70, 0), new Vector2(60, 60));
            clock = Ui.Label(bar2.transform, "1:40", 30, true, TextAnchor.MiddleCenter, Ui.Ink, center, center, new Vector2(0, 8), new Vector2(80, 40));
            roundText = Ui.Label(bar2.transform, "ROUND 1", 18, false, TextAnchor.MiddleCenter, Ui.Dim, center, center, new Vector2(0, -20), new Vector2(120, 24));
            scoreOpp = Ui.Label(bar2.transform, "0", 52, true, TextAnchor.MiddleCenter, Ui.Ink, center, center, new Vector2(70, 0), new Vector2(60, 60));
            nameOpp = Ui.Label(bar2.transform, "OPPONENT", 24, true, TextAnchor.MiddleLeft, Ui.Red, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(170, 40));

            // Centre banner
            bigText = Ui.Label(root, "", 110, true, TextAnchor.MiddleCenter, Ui.Ink, center, center, new Vector2(0, 170), new Vector2(1400, 140));
            bigText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3, -3);
            subText = Ui.Label(root, "", 32, false, TextAnchor.MiddleCenter, Ui.Amber, center, center, new Vector2(0, 92), new Vector2(1400, 50));
            subText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            noticeText = Ui.Label(root, "", 26, false, TextAnchor.MiddleCenter, Ui.Ink, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(1400, 40));

            // Health, bottom left
            var bl = new Vector2(0, 0);
            var hpPanel = Ui.Fill("Health", root, Ui.Panel, bl, bl, new Vector2(40, 40), new Vector2(330, 92));
            hpText = Ui.Label(hpPanel.transform, "100", 64, true, TextAnchor.MiddleLeft, Ui.Ink, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 6), new Vector2(130, 70));
            Ui.Label(hpPanel.transform, "HEALTH", 18, false, TextAnchor.MiddleLeft, Ui.Dim, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 18), new Vector2(160, 24));
            Ui.Fill("HpTrack", hpPanel.transform, new Color(1, 1, 1, 0.12f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, -10), new Vector2(160, 8));
            hpFill = Ui.Fill("HpFill", hpPanel.transform, Ui.Cyan, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, -10), new Vector2(160, 8));

            // Ammo, bottom right
            var br = new Vector2(1, 0);
            var ammoPanel = Ui.Fill("Ammo", root, Ui.Panel, br, br, new Vector2(-40, 40), new Vector2(330, 92));
            ammoText = Ui.Label(ammoPanel.transform, "30", 64, true, TextAnchor.MiddleRight, Ui.Ink, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, 6), new Vector2(130, 70));
            reserveText = Ui.Label(ammoPanel.transform, "/ 90", 28, false, TextAnchor.MiddleLeft, Ui.Dim, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, -4), new Vector2(120, 40));
            weaponText = Ui.Label(ammoPanel.transform, "RIFLE", 18, false, TextAnchor.MiddleLeft, Ui.Amber, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 26), new Vector2(120, 24));
            reloadText = Ui.Label(root, "RELOADING", 24, true, TextAnchor.MiddleCenter, Ui.Amber, center, center, new Vector2(0, -70), new Vector2(300, 30));

            BuildPause(root);
            BuildEnd(root);
            canvas.gameObject.SetActive(false);
        }

        void BuildPause(Transform root)
        {
            var c = new Vector2(0.5f, 0.5f);
            var p = Ui.Stretch("Pause", root);
            Ui.Box(p, new Color(0.03f, 0.02f, 0.01f, 0.6f), true);
            Ui.Label(p, "PAUSED", 90, true, TextAnchor.MiddleCenter, Ui.Ink, c, c, new Vector2(0, 150), new Vector2(800, 120));
            Ui.Label(p, "Click anywhere to return to the fight", 30, false, TextAnchor.MiddleCenter, Ui.Dim, c, c, new Vector2(0, 80), new Vector2(900, 40));
            Ui.Label(p, "MOUSE SENSITIVITY", 20, false, TextAnchor.MiddleCenter, Ui.Dim, c, c, new Vector2(0, 10), new Vector2(400, 30));
            Ui.Slider(p, 0.2f, 3f, LocalPlayer.Sensitivity, v => { LocalPlayer.Sensitivity = v; PlayerPrefs.SetFloat("sens", v); },
                c, c, new Vector2(0, -24), new Vector2(380, 30));
            Ui.Button(p, "LEAVE MATCH", () => OnLeave?.Invoke(), false, c, c, new Vector2(0, -110), new Vector2(320, 64));
            pausePanel = p.gameObject;
            pausePanel.SetActive(false);
        }

        void BuildEnd(Transform root)
        {
            var c = new Vector2(0.5f, 0.5f);
            var p = Ui.Stretch("End", root);
            Ui.Box(p, new Color(0.03f, 0.02f, 0.01f, 0.55f), true);
            endTitle = Ui.Label(p, "VICTORY", 150, true, TextAnchor.MiddleCenter, Ui.Amber, c, c, new Vector2(0, 150), new Vector2(1400, 180));
            endScore = Ui.Label(p, "3 - 1", 64, true, TextAnchor.MiddleCenter, Ui.Ink, c, c, new Vector2(0, 40), new Vector2(800, 80));
            endReason = Ui.Label(p, "", 28, false, TextAnchor.MiddleCenter, Ui.Dim, c, c, new Vector2(0, -20), new Vector2(1000, 40));
            Ui.Button(p, "PLAY AGAIN", () => OnPlayAgain?.Invoke(), true, c, c, new Vector2(-175, -120), new Vector2(320, 70));
            Ui.Button(p, "MAIN MENU", () => OnLeave?.Invoke(), false, c, c, new Vector2(175, -120), new Vector2(320, 70));
            endPanel = p.gameObject;
            endPanel.SetActive(false);
        }

        public void Show(string you, string opp)
        {
            canvas.gameObject.SetActive(true);
            nameYou.text = you.ToUpperInvariant();
            nameOpp.text = opp.ToUpperInvariant();
            scoreYou.text = scoreOpp.text = "0";
            endPanel.SetActive(false);
            pausePanel.SetActive(false);
            bigText.text = subText.text = "";
            hurt = 0;
        }

        public void Hide() => canvas.gameObject.SetActive(false);

        public void SetScore(int you, int opp, int round)
        {
            scoreYou.text = you.ToString();
            scoreOpp.text = opp.ToString();
            roundText.text = "ROUND " + round;
        }

        public void SetClock(float left) => clock.text = Ui.Clock(left);

        public void Banner(string big, string sub, Color color, float seconds)
        {
            bigText.text = big;
            bigText.color = color;
            subText.text = sub;
            bigUntil = seconds > 0 ? Time.time + seconds : -1f;
        }

        public void Notice(string text, float seconds)
        {
            noticeText.text = text;
            noticeUntil = Time.time + seconds;
        }

        public void SetPaused(bool paused) => pausePanel.SetActive(paused && !endPanel.activeSelf);

        public void End(bool won, int you, int opp, string reason)
        {
            endPanel.SetActive(true);
            pausePanel.SetActive(false);
            endTitle.text = won ? "VICTORY" : "DEFEAT";
            endTitle.color = won ? Ui.Amber : Ui.Red;
            endScore.text = you + "  -  " + opp;
            endReason.text = reason;
            bigText.text = subText.text = "";
        }

        public void HitMarker(bool head, int dmg)
        {
            hitMarker.gameObject.SetActive(true);
            hitUntil = Time.time + (head ? 0.3f : 0.18f);
            foreach (var b in hitBars) b.color = head ? Ui.Red : Ui.Ink;
            hitMarker.localScale = Vector3.one * (head ? 1.35f : 1f);
            dmgText.text = (head ? "HEADSHOT  " : "") + dmg;
            dmgText.color = head ? Ui.Red : Ui.Ink;
            dmgUntil = Time.time + 0.7f;
        }

        public void Damaged(int amount) => hurt = Mathf.Min(1f, hurt + amount / 45f);

        public void Tick(LocalPlayer p)
        {
            if (!canvas.gameObject.activeSelf) return;
            if (bigUntil > 0 && Time.time > bigUntil) { bigText.text = subText.text = ""; bigUntil = -1; }
            if (Time.time > noticeUntil) noticeText.text = "";
            if (hitMarker.gameObject.activeSelf && Time.time > hitUntil) hitMarker.gameObject.SetActive(false);
            if (Time.time > dmgUntil) dmgText.text = "";
            hurt = Mathf.MoveTowards(hurt, 0f, Time.deltaTime * 0.8f);
            vignette.color = new Color(0.75f, 0.05f, 0.02f, hurt * 0.85f + (p != null && p.Alive && p.Hp < 35 ? 0.25f : 0f));
            if (p == null) return;

            hpText.text = p.Hp.ToString();
            hpText.color = p.Hp < 35 ? Ui.Red : Ui.Ink;
            hpFill.rectTransform.sizeDelta = new Vector2(160f * p.Hp / 100f, 8);
            hpFill.color = p.Hp < 35 ? Ui.Red : Ui.Cyan;
            var w = Weapons.All[p.Weapon];
            ammoText.text = p.Mag[p.Weapon].ToString();
            ammoText.color = p.Mag[p.Weapon] <= w.Mag / 4 ? Ui.Red : Ui.Ink;
            reserveText.text = "/ " + p.Reserve[p.Weapon];
            weaponText.text = w.Name;
            reloadText.gameObject.SetActive(p.Reloading && p.Alive);

            // Crosshair gap follows the actual spread cone.
            bool show = p.Alive && !p.Ads;
            cross.gameObject.SetActive(show);
            if (show)
            {
                float px = Mathf.Tan(p.Spread * Mathf.Deg2Rad) / Mathf.Tan(p.Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 540f;
                float gap = Mathf.Clamp(px, 4f, 90f) + 6f;
                crossBars[0].anchoredPosition = new Vector2(0, gap);
                crossBars[1].anchoredPosition = new Vector2(0, -gap);
                crossBars[2].anchoredPosition = new Vector2(gap, 0);
                crossBars[3].anchoredPosition = new Vector2(-gap, 0);
            }
        }
    }
}
