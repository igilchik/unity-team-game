using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace IslandTycoon.EditorTools
{
    /// <summary>
    /// SCRUM-173: one-click setup of the Android platform.
    /// Menu: Island Tycoon / Android / ...
    /// </summary>
    public static class AndroidBuildSetup
    {
        const string AppId = "sk.ukf.islandtycoon";

        [MenuItem("Island Tycoon/Android/Switch Platform to Android")]
        public static void SwitchToAndroid()
        {
            ApplySettings();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log("[IslandTycoon] Active platform: Android");
        }

        [MenuItem("Island Tycoon/Android/Apply Player Settings")]
        public static void ApplySettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "UKF";
            PlayerSettings.productName = "Island Tycoon";
            PlayerSettings.SetApplicationIdentifier(android, AppId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            Debug.Log("[IslandTycoon] Android player settings applied");
        }

        [MenuItem("Island Tycoon/Android/Build APK")]
        public static void BuildApk()
        {
            SwitchToAndroid();
            EditorUserBuildSettings.buildAppBundle = false;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var path = EditorUtility.SaveFilePanel("Build APK", "Builds", "IslandTycoon.apk", "apk");
            if (string.IsNullOrEmpty(path)) return;
            var report = BuildPipeline.BuildPlayer(scenes, path, BuildTarget.Android, BuildOptions.None);
            Debug.Log("[IslandTycoon] Build " + report.summary.result + ": " + path);
        }
    }
}
