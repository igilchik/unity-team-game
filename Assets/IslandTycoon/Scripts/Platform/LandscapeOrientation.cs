using System;
using UnityEngine;

namespace IslandTycoon
{
    /// <summary>
    /// SCRUM-174: the game runs only in landscape. The phone may flip between
    /// landscape-left and landscape-right, portrait is never used.
    /// Also holds the shared landscape layout values for all UI canvases.
    /// </summary>
    public class LandscapeOrientation : MonoBehaviour
    {
        /// <summary>Reference resolution of every canvas (16:9 landscape).</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        /// <summary>CanvasScaler match: 1 = scale by height, so wider phones (19.5:9, 20:9) get extra space on the sides.</summary>
        public const float MatchHeight = 1f;

        /// <summary>Raised when the screen flips or changes size (landscape-left / right, split screen, editor Game view).</summary>
        public static event Action Changed;

        public static bool IsLandscape => Screen.width >= Screen.height;

        /// <summary>Screen aspect, width / height (always at least 1 in landscape).</summary>
        public static float Aspect => Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;

        ScreenOrientation _lastOrientation;
        Vector2Int _lastSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Apply();
            var go = new GameObject("LandscapeOrientation");
            DontDestroyOnLoad(go);
            go.AddComponent<LandscapeOrientation>();
        }

        /// <summary>Allow only the two landscape orientations and let the sensor pick between them.</summary>
        public static void Apply()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            if (Application.isMobilePlatform)
            {
                // force landscape first so the app never starts in portrait, then enable auto-rotation
                if (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown)
                    Screen.orientation = ScreenOrientation.LandscapeLeft;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
        }

        void Start()
        {
            _lastOrientation = Screen.orientation;
            _lastSize = new Vector2Int(Screen.width, Screen.height);
        }

        void Update()
        {
            if (Screen.orientation == _lastOrientation && Screen.width == _lastSize.x && Screen.height == _lastSize.y) return;
            _lastOrientation = Screen.orientation;
            _lastSize = new Vector2Int(Screen.width, Screen.height);
            Changed?.Invoke();
        }
    }
}
