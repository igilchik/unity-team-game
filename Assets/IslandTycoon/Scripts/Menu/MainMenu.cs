using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>
    /// SCRUM-172: main menu of the game. Built from code at start-up in whatever
    /// scene is open, so nothing has to be wired by hand in the editor.
    /// Sky background, logo, PLAY button, settings and help buttons.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public static MainMenu Instance { get; private set; }

        /// <summary>Raised when the player presses PLAY (the game hooks in here).</summary>
        public static event Action PlayPressed;

        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
        static readonly Color SkyBlue = new Color(0.13f, 0.56f, 0.98f);

        RectTransform _menu, _logo;
        Vector2 _logoBase;
        Text _toast;
        float _toastTime;

        public bool IsOpen => _menu != null && _menu.gameObject.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<MainMenu>() != null) return;
            new GameObject("MainMenu").AddComponent<MainMenu>();
        }

        void Awake()
        {
            Instance = this;
            EnsureEventSystem();
            Build();
            Show();
        }

        public void Show()
        {
            _menu.gameObject.SetActive(true);
            _menu.SetAsLastSibling();
        }

        public void Hide() => _menu.gameObject.SetActive(false);

        public void ShowToast(string msg)
        {
            _toast.text = msg;
            _toastTime = 2.5f;
            _toast.transform.parent.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // full-screen backdrop
            _menu = NewRect("Menu", canvasGo.transform, Mid, Vector2.zero, Vector2.zero);
            Stretch(_menu);
            _menu.gameObject.AddComponent<Image>().color = SkyBlue;

            // sky fills the whole screen whatever its aspect
            var sky = Art.TryGet("Backgrounds/sky");
            if (sky != null)
            {
                var skyImg = NewImage("Sky", _menu, sky, Mid, Vector2.zero, new Vector2(1920, 1080));
                skyImg.preserveAspect = false;
                var fit = skyImg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = sky.rect.width / sky.rect.height;
            }

            // buttons and logo live in their own layer so they can be kept inside the screen
            var content = NewRect("Content", _menu, Mid, Vector2.zero, Vector2.zero);
            Stretch(content);

            var logo = Art.TryGet("UI/logo");
            if (logo != null)
            {
                var l = NewImage("Logo", content, logo, Mid, new Vector2(0, 175), new Vector2(980, 411));
                l.preserveAspect = true;
                _logo = l.rectTransform;
            }
            else
            {
                _logo = NewText("Logo", content, "ISLAND TYCOON", 130, new Color(1f, 0.75f, 0.3f), Mid, new Vector2(0, 200), new Vector2(1400, 170)).rectTransform;
            }
            _logoBase = _logo.anchoredPosition;

            MakeButton("Play", content, "UI/Kit/btn_primary_orange", "PLAY", 66, new Vector2(0, -175), 440, OnPlay);
            MakeIconButton("Settings", content, "UI/Icons/icon_settings", "II", new Vector2(-350, -175), 136, () => ShowToast("Settings: coming soon"));
            MakeIconButton("Help", content, "UI/Icons/icon_help", "?", new Vector2(350, -175), 136, () => ShowToast("Help: coming soon"));

            NewText("Version", content, "v" + Application.version, 26, new Color(1, 1, 1, 0.75f),
                new Vector2(1, 0), new Vector2(-110, 40), new Vector2(200, 40));

            // toast sits above everything, also when the menu is hidden
            var toastLayer = NewRect("ToastLayer", canvasGo.transform, Mid, Vector2.zero, Vector2.zero);
            Stretch(toastLayer);
            _toast = NewText("Toast", toastLayer, "", 36, Color.white, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(1200, 70));
            var shadow = _toast.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(0, -3);
        }

        void OnPlay()
        {
            Hide();
            if (PlayPressed != null) PlayPressed.Invoke();
            else ShowToast("The game starts here (next sprint)");
        }

        void Update()
        {
            // gentle logo bob
            if (_logo != null && IsOpen)
                _logo.anchoredPosition = _logoBase + new Vector2(0, Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / 2.2f) * 10f);

            if (_toastTime > 0)
            {
                _toastTime -= Time.unscaledDeltaTime;
                if (_toastTime <= 0) _toast.text = "";
            }
        }

        // ------------------------------------------------------------------ helpers

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // new Input System if the project uses it, otherwise the classic module
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) go.AddComponent(t);
            else go.AddComponent<StandaloneInputModule>();
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = Mid;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = NewRect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static Text NewText(string name, Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var t = NewRect(name, parent, anchor, pos, box).gameObject.AddComponent<Text>();
            t.font = Art.Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = text;
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = size;
            t.resizeTextMinSize = Mathf.Max(14, size / 2);
            return t;
        }

        /// <summary>Kit button; the height follows the art so it is never stretched.</summary>
        static Button MakeButton(string name, Transform parent, string spritePath, string label, int fontSize, Vector2 pos, float width, Action onClick)
        {
            var sprite = Art.Get(spritePath);
            float h = sprite != null ? width * sprite.rect.height / sprite.rect.width : width * 0.4f;
            var img = NewImage(name, parent, sprite, Mid, pos, new Vector2(width, h));
            img.raycastTarget = true;
            img.preserveAspect = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick?.Invoke());

            // label inside the glass part of the orange button art
            var t = NewText("Label", img.transform, label, fontSize, Color.white, Mid, Vector2.zero, Vector2.zero);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.25f, 0.30f);
            rt.anchorMax = new Vector2(0.786f, 0.724f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.32f, 0.16f, 0.04f, 0.55f);
            sh.effectDistance = new Vector2(0, -2.5f);
            return b;
        }

        static Button MakeIconButton(string name, Transform parent, string iconPath, string fallback, Vector2 pos, float size, Action onClick)
        {
            var icon = Art.TryGet(iconPath);
            var img = NewImage(name, parent, icon, Mid, pos, new Vector2(size, size));
            img.raycastTarget = true;
            img.preserveAspect = true;
            if (icon == null)
            {
                img.color = new Color(0.36f, 0.20f, 0.07f);
                NewText("Label", img.transform, fallback, 48, Color.white, Mid, Vector2.zero, new Vector2(size, size));
            }
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick?.Invoke());
            return b;
        }
    }
}
