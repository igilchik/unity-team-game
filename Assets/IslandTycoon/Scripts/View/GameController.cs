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

        /// <summary>The game waits while the main menu covers it.</summary>
        public bool Paused => MainMenu.Instance != null && MainMenu.Instance.IsOpen;

        Camera _cam;
        WorldView _world;

        void Awake()
        {
            Model = new GameModel(GameConfig.Load());

            SetupCamera();
            SetupEventSystem();
            Rig = new CameraRig(_cam);

            _world = new GameObject("World").AddComponent<WorldView>();
            _world.transform.SetParent(transform, false);
            _world.Build(this);

            MainMenu.PlayPressed += OnPlay;
        }

        void OnDestroy() => MainMenu.PlayPressed -= OnPlay;

        /// <summary>PLAY in the main menu: show the island from its default view.</summary>
        void OnPlay() => Rig.Reset();

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
        }
    }
}
