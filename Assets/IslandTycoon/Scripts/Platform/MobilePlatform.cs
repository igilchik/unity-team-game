using System;
using UnityEngine;

namespace IslandTycoon
{
    /// <summary>
    /// SCRUM-173: runtime settings for the mobile (Android) platform.
    /// Applied automatically before the first scene loads.
    /// </summary>
    public class MobilePlatform : MonoBehaviour
    {
        public const int TargetFps = 60;

        /// <summary>Android system Back button (Escape in the editor and on desktop).</summary>
        public static event Action BackPressed;

        public static bool IsMobile => Application.isMobilePlatform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            // smooth 60 fps on phones (Android defaults to 30)
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;

            // the screen must not dim while the player is watching the resort
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // pinch zoom and multi-finger input
            Input.multiTouchEnabled = true;

            // full screen without the system bars
            if (IsMobile) Screen.fullScreen = true;

            var go = new GameObject("MobilePlatform");
            DontDestroyOnLoad(go);
            go.AddComponent<MobilePlatform>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) BackPressed?.Invoke();
        }

        /// <summary>Closes the app on a phone; stops Play mode in the editor.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
