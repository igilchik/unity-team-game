using System;
using UnityEngine;

namespace IslandTycoon
{
    [Serializable]
    public class IslandConfig
    {
        public string name = "Palm Island";
        [Tooltip("Sprite key under Resources/IslandTycoon/Sprites")]
        public string background = "Backgrounds/island_main";
        [Tooltip("Size of the background art in pixels (used when the sprite is missing)")]
        public Vector2 backgroundPixels = new Vector2(1672, 941);
    }

    /// <summary>
    /// All tunable numbers live here, separate from game logic.
    /// Create an asset (Assets > Create > Island Tycoon > Game Config) in
    /// Resources/IslandTycoon to override the defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Island Tycoon/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Island")]
        public IslandConfig island = new IslandConfig();

        public const string ResourcePath = "IslandTycoon/GameConfig";

        public static GameConfig Load()
        {
            var cfg = Resources.Load<GameConfig>(ResourcePath);
            return cfg != null ? cfg : CreateInstance<GameConfig>();
        }
    }
}
