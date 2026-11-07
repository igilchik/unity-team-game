using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IslandTycoon
{
    /// <summary>
    /// Creates the game at runtime in whatever scene is open, so nothing
    /// has to be wired by hand in the editor. Just press Play.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (UnityEngine.Object.FindAnyObjectByType<GameController>() != null) return;
            new GameObject("IslandTycoon").AddComponent<GameController>();
        }
    }

    /// <summary>
    /// SCRUM-15: Home / Island View. Connects the model with the island view and the camera.
    /// The island sits behind the main menu; PLAY hides the menu and shows it.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public GameModel Model { get; private set; }
        public CameraRig Rig { get; private set; }
        /// <summary>Building whose worker panel is open, -1 = none.</summary>
        public int Selected { get; private set; } = -1;

        /// <summary>The game waits while the main menu covers it.</summary>
        public bool Paused => MainMenu.Instance != null && MainMenu.Instance.IsOpen;

        Camera _cam;
        WorldView _world;
        HudView _hud;
        NeedsPanel _needs;
        WorkerPanel _workers;

        void Awake()
        {
            Model = new GameModel(GameConfig.Load());

            SetupCamera();
            SetupEventSystem();
            Rig = new CameraRig(_cam);

            _world = new GameObject("World").AddComponent<WorldView>();
            _world.transform.SetParent(transform, false);
            _world.Build(this);

            _hud = new GameObject("Hud").AddComponent<HudView>();
            _hud.transform.SetParent(transform, false);
            _hud.Build(Model, StartWave);

            _needs = _hud.gameObject.AddComponent<NeedsPanel>();
            _needs.Build(Model, _hud.Root);

            _workers = _hud.gameObject.AddComponent<WorkerPanel>();
            _workers.Build(Model, _hud.Root, () => Selected, MoveWorker, () => SelectBuilding(-1));

            MainMenu.PlayPressed += OnPlay;
        }

        void OnDestroy() => MainMenu.PlayPressed -= OnPlay;

        /// <summary>PLAY in the main menu: show the island from its default view.</summary>
        void OnPlay() => Rig.Reset();

        // ------------------------------------------------------------------ commands from the UI

        /// <summary>START WAVE button.</summary>
        public void StartWave() => Model.StartWave();

        /// <summary>Tap on a building (again = close), -1 closes the worker panel.</summary>
        public void SelectBuilding(int need)
        {
            if (Paused) return;
            Selected = need < 0 || Selected == need ? -1 : need;
        }

        /// <summary>- / + in the worker panel for the selected building.</summary>
        public void MoveWorker(int delta)
        {
            if (Selected < 0) return;
            if (delta > 0) Model.PullWorker(Selected);
            else Model.MoveWorker(Selected, -1);
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.transform.position = new Vector3(0, 0, -10);
            _cam.transform.rotation = Quaternion.identity;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.62f, 0.78f);
            // world sprites receive taps and drags through 2D physics
            if (_cam.GetComponent<Physics2DRaycaster>() == null) _cam.gameObject.AddComponent<Physics2DRaycaster>();
            if (_cam.GetComponent<AudioListener>() == null && UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
                _cam.gameObject.AddComponent<AudioListener>();
        }

        static void SetupEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // new Input System if the project uses it, otherwise the classic module
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) go.AddComponent(t);
            else go.AddComponent<StandaloneInputModule>();
        }

        void Update()
        {
            if (Paused) return;
            Rig.Update();
            Model.Tick(Time.deltaTime);
        }
    }
}
