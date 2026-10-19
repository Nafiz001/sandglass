using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sandglass.UI
{
    /// <summary>Small helpers for building uGUI in code with one visual style.</summary>
    public static class Ui
    {
        public static readonly Color Ink = new Color(0.98f, 0.95f, 0.89f);
        public static readonly Color Dim = new Color(0.98f, 0.95f, 0.89f, 0.6f);
        public static readonly Color Amber = new Color(1f, 0.71f, 0.33f);
        public static readonly Color Cyan = new Color(0.36f, 0.9f, 1f);
        public static readonly Color Red = new Color(1f, 0.34f, 0.27f);
        public static readonly Color Panel = new Color(0.07f, 0.05f, 0.04f, 0.72f);

        static Font bold, medium;
        public static Font Bold => bold != null ? bold : bold = Resources.Load<Font>("Fonts/Rajdhani-Bold");
        public static Font Medium => medium != null ? medium : medium = Resources.Load<Font>("Fonts/Rajdhani-Medium");

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var r = Rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        public static Text Label(Transform parent, string text, int size, bool isBold, TextAnchor align, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = isBold ? Bold : Medium;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return t;
        }

        public static Text Label(Transform parent, string text, int size, bool isBold, TextAnchor align, Color color,
                                 Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box)
        {
            var r = Rect("Text", parent, anchor, pivot, pos, box);
            return Label(r, text, size, isBold, align, color);
        }

        public static Image Box(Transform parent, Color color, bool raycast = false)
        {
            var img = parent.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Fill(string name, Transform parent, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = Rect(name, parent, anchor, pivot, pos, size);
            return Box(r, color);
        }

        public static Button Button(Transform parent, string label, Action onClick, bool primary,
                                    Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = Rect("Button " + label, parent, anchor, pivot, pos, size);
            var img = Box(r, primary ? Amber : new Color(1f, 0.97f, 0.9f, 0.1f), true);
            var b = r.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            var t = Label(r, label, 28, true, TextAnchor.MiddleCenter, primary ? new Color(0.12f, 0.08f, 0.04f) : Ink);
            if (!primary)
            {
                var edge = Fill("Edge", r, new Color(1f, 0.71f, 0.33f, 0.9f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, size.y));
                edge.raycastTarget = false;
            }
            return b;
        }

        public static InputField Input(Transform parent, string placeholder, int limit,
                                       Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = Rect("Input", parent, anchor, pivot, pos, size);
            var img = Box(r, new Color(0f, 0f, 0f, 0.45f), true);
            var f = r.gameObject.AddComponent<InputField>();
            f.targetGraphic = img;
            f.characterLimit = limit;
            var textR = Rect("Text", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            textR.anchorMin = Vector2.zero;
            textR.anchorMax = Vector2.one;
            textR.offsetMin = new Vector2(18, 0);
            textR.offsetMax = new Vector2(-18, 0);
            var text = Label(textR, "", 28, false, TextAnchor.MiddleLeft, Ink);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var ph = Label(textR, placeholder, 28, false, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.35f));
            f.textComponent = text;
            f.placeholder = ph;
            return f;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange,
                                    Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = Rect("Slider", parent, anchor, pivot, pos, size);
            var track = Fill("Track", r, new Color(1, 1, 1, 0.15f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x, 6));
            var fillArea = Rect("FillArea", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x, 6));
            var fill = Fill("Fill", fillArea, Amber, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 6));
            var handleArea = Rect("HandleArea", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x, size.y));
            var handle = Fill("Handle", handleArea, Ink, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 28));
            handle.raycastTarget = true;
            var s = r.gameObject.AddComponent<Slider>();
            s.fillRect = (RectTransform)fill.transform;
            s.handleRect = (RectTransform)handle.transform;
            s.targetGraphic = handle;
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange(v));
            track.raycastTarget = true;
            return s;
        }

        static Sprite radial;

        /// <summary>A sprite that is clear in the middle and opaque at the edges.</summary>
        public static Sprite Vignette()
        {
            if (radial != null) return radial;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, (d - 0.45f) / 0.65f)));
                }
            tex.Apply();
            radial = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return radial;
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
