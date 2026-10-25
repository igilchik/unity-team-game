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
    /// Uses the universal UI elements from UIFactory (SCRUM-175).
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public static MainMenu Instance { get; private set; }

        /// <summary>Raised when the player presses PLAY (the game hooks in here).</summary>
        public static event Action PlayPressed;

        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
        static readonly Color SkyBlue = new Color(0.13f, 0.56f, 0.98f);

        RectTransform _root, _menu, _settingsModal, _helpModal;
        Text _toast;
        float _toastTime, _exitArmed;

        public bool IsOpen => UIFactory.IsOpen(_menu);

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
            MobilePlatform.BackPressed += OnBack;
        }

        void OnDestroy() => MobilePlatform.BackPressed -= OnBack;

        /// <summary>Android Back: close a dialog, return to the menu, or press twice in the menu to exit.</summary>
        void OnBack()
        {
            if (UIFactory.IsOpen(_settingsModal) || UIFactory.IsOpen(_helpModal))
            {
                UIFactory.Close(_settingsModal);
                UIFactory.Close(_helpModal);
                return;
            }
            if (!IsOpen) { Show(); return; }
            if (_exitArmed > 0) { MobilePlatform.Quit(); return; }
            _exitArmed = 2f;
            ShowToast("Press back again to exit");
        }

        public void Show() => UIFactory.Open(_menu);

        public void Hide()
        {
            UIFactory.Close(_settingsModal);
            UIFactory.Close(_helpModal);
            UIFactory.Close(_menu);
        }

        public void ShowToast(string msg)
        {
            UIFactory.SetText(_toast, msg);
            _toastTime = 2.5f;
            _toast.transform.parent.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            var canvas = UIFactory.Canvas("MenuCanvas", 100);
            canvas.transform.SetParent(transform, false);
            _root = (RectTransform)canvas.transform;

            // full-screen backdrop
            _menu = UIFactory.Rect("Menu", _root, Mid, Vector2.zero, Vector2.zero);
            UIFactory.Stretch(_menu);
            _menu.gameObject.AddComponent<Image>().color = SkyBlue;

            // sky fills the whole screen whatever its aspect
            var sky = Art.TryGet("Backgrounds/sky");
            if (sky != null)
            {
                var skyImg = UIFactory.Image("Sky", _menu, sky, Mid, Vector2.zero, new Vector2(1920, 1080));
                skyImg.preserveAspect = false;
                skyImg.rectTransform.pivot = Mid;
                var fit = skyImg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = sky.rect.width / sky.rect.height;
                skyImg.gameObject.AddComponent<Drift>().Amount = new Vector3(0.02f, 0, 6f);
            }

            // buttons and logo stay inside the safe area of the phone screen
            var content = UIFactory.Rect("Content", _menu, Mid, Vector2.zero, Vector2.zero);
            UIFactory.Stretch(content);
            content.gameObject.AddComponent<SafeArea>();

            var logo = Art.TryGet("UI/logo");
            if (logo != null)
            {
                var l = UIFactory.Icon("Logo", content, logo, Mid, new Vector2(0, 175), 1);
                l.rectTransform.sizeDelta = new Vector2(980, 411);
                l.gameObject.AddComponent<Drift>().Amount = new Vector3(0, 10f, 2.2f);
            }
            else
            {
                UIFactory.Label("Logo", content, "ISLAND TYCOON", 130, new Color(1f, 0.75f, 0.3f), Mid, new Vector2(0, 200),
                    new Vector2(1400, 170), TextAnchor.MiddleCenter, true).gameObject.AddComponent<Drift>().Amount = new Vector3(0, 10f, 2.2f);
            }

            UIFactory.Button("Play", content, "UI/Kit/btn_primary_orange", "PLAY", 66, Mid, new Vector2(0, -175), new Vector2(440, 0), OnPlay, true);
            UIFactory.IconButton("MenuSettings", content, "UI/Icons/icon_settings", "II", Mid, new Vector2(-350, -175), new Vector2(136, 136),
                () => UIFactory.Open(_settingsModal), true);
            UIFactory.IconButton("MenuHelp", content, "UI/Icons/icon_help", "?", Mid, new Vector2(350, -175), new Vector2(136, 136),
                () => UIFactory.Open(_helpModal), true);

            var pill = UIFactory.WoodPill("VersionPill", content, Mid, new Vector2(0, -360), new Vector2(520, 84), 84f, out var pillInner).rectTransform;
            pill.pivot = Mid;
            UIFactory.FitText("Version", pillInner, "Island Tycoon  v" + Application.version, 28, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, true);

            BuildSettingsModal();
            BuildHelpModal();

            // toast sits above everything, also when the menu is hidden
            var toastLayer = UIFactory.Rect("ToastLayer", _root, Mid, Vector2.zero, Vector2.zero);
            UIFactory.Stretch(toastLayer);
            toastLayer.gameObject.AddComponent<SafeArea>();
            _toast = UIFactory.Label("Toast", toastLayer, "", 36, Color.white, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(1200, 70), TextAnchor.MiddleCenter, true);
        }

        void BuildSettingsModal()
        {
            _settingsModal = UIFactory.Modal("SettingsModal", _menu, new Vector2(900, 620), "SETTINGS", out var p);
            UIFactory.Label("Text", p, "Sound, music and vibration settings\nwill be added here.", 34, UIFactory.Brown,
                Mid, new Vector2(0, 40), new Vector2(720, 200));
            UIFactory.Button("Ok", p, "UI/Kit/btn_secondary_teal", "OK", 40, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 0),
                () => UIFactory.Close(_settingsModal), true);
            UIFactory.CloseButton(_settingsModal);
        }

        void BuildHelpModal()
        {
            _helpModal = UIFactory.Modal("HelpModal", _menu, new Vector2(1000, 680), "HOW TO PLAY", out var p);
            UIFactory.Label("Text", p,
                "Build your island resort, serve every guest\nand upgrade buildings to earn more coins.",
                32, UIFactory.Brown, Mid, new Vector2(0, 50), new Vector2(820, 220));
            UIFactory.Button("Ok", p, "UI/Kit/btn_primary_orange", "GOT IT", 40, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(320, 0),
                () => UIFactory.Close(_helpModal), true);
            UIFactory.CloseButton(_helpModal);
        }

        void OnPlay()
        {
            Hide();
            if (PlayPressed != null) PlayPressed.Invoke();
            else ShowToast("The game starts here (next sprint)");
        }

        void Update()
        {
            if (_exitArmed > 0) _exitArmed -= Time.unscaledDeltaTime;

            if (_toastTime > 0)
            {
                _toastTime -= Time.unscaledDeltaTime;
                if (_toastTime <= 0) UIFactory.SetText(_toast, "");
            }
        }

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
    }
}
