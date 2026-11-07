using UnityEditor;
using UnityEngine;

namespace IslandTycoon.EditorTools
{
    /// <summary>Menu Island Tycoon > Run Self Test: runs the game checks and prints the report to the Console.</summary>
    public static class SelfTestMenu
    {
        [MenuItem("Island Tycoon/Run Self Test")]
        public static void Run()
        {
            string report = SelfTest.Run();
            if (SelfTest.Passed) Debug.Log("[IslandTycoon] Self test\n" + report);
            else Debug.LogError("[IslandTycoon] Self test\n" + report);
        }
    }
}
