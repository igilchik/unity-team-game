using System;
using UnityEngine;

namespace IslandTycoon
{
    [Serializable]
    public class NeedConfig
    {
        public NeedType need;
        public string buildingName;
        [Tooltip("Sprite key: Buildings/<key>_lv1")]
        public string spriteKey;

        [Header("Guest need")]
        [Tooltip("Guests can ask for this need (its building must be open)")]
        public bool enabled;
        public string displayName;
        [Tooltip("Icon sprite key, shown in guest bubbles and panels")]
        public string icon;
        [Tooltip("Seconds to serve one guest")]
        public float baseServiceTime = 4f;
        [Tooltip("How often guests ask for this need compared to the others")]
        public float weight = 1f;
    }

    [Serializable]
    public class PlotConfig
    {
        public NeedType need;
        [Tooltip("Centre of the sand plot in background pixels")]
        public Vector2 pixel;
        [Tooltip("Plot width in background pixels")]
        public float width = 250;
        [Tooltip("Completed waves before the area can be opened (0 = open from start)")]
        public int unlockAfterWave;
        [Tooltip("Locked area is overgrown jungle instead of a fence")]
        public bool jungleLock;
    }

    [Serializable]
    public class IslandConfig
    {
        public string name = "Palm Island";
        [Tooltip("Sprite key under Resources/IslandTycoon/Sprites")]
        public string background = "Backgrounds/island_main";
        [Tooltip("Size of the background art in pixels (used when the sprite is missing)")]
        public Vector2 backgroundPixels = new Vector2(1672, 941);
        public PlotConfig[] plots =
        {
            new PlotConfig { need = NeedType.Rooms,     pixel = new Vector2(460, 355),  width = 250 },
            new PlotConfig { need = NeedType.Drinks,    pixel = new Vector2(435, 570),  width = 270 },
            new PlotConfig { need = NeedType.Food,      pixel = new Vector2(1160, 615), width = 270 },
            new PlotConfig { need = NeedType.Relax,     pixel = new Vector2(1230, 395), width = 255, unlockAfterWave = 3 },
            new PlotConfig { need = NeedType.OceanView, pixel = new Vector2(835, 235),  width = 250, unlockAfterWave = 6, jungleLock = true },
        };
    }

    /// <summary>How many guests come in a wave and what they ask for.</summary>
    [Serializable]
    public class WaveConfig
    {
        public int guests = 5;
        [Tooltip("Seconds between guest arrivals")]
        public float spacing = 3f;
        [Tooltip("Needs per guest (each guest asks for different needs)")]
        public int minNeeds = 1;
        public int maxNeeds = 1;
        [Tooltip("Per-need weight multipliers: Rooms, Drinks, Food, Relax, OceanView")]
        public float[] weights = { 1, 1, 1, 1, 1 };
    }

    /// <summary>
    /// All tunable numbers live here, separate from game logic.
    /// Create an asset (Assets > Create > Island Tycoon > Game Config) in
    /// Resources/IslandTycoon to override the defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Island Tycoon/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Start")]
        public int startCoins = 60;
        public int startEnergy = 6;

        [Header("Guests")]
        [Tooltip("Seconds a guest walks from the boat / between buildings")]
        public float walkTime = 2f;
        [Tooltip("Guests served at the same time by one building")]
        public int serviceSlots = 1;
        [Tooltip("Seconds a guest is willing to wait in a queue")]
        public float patience = 12f;

        [Header("Bottleneck")]
        [Tooltip("This many guests waiting at one building = bottleneck")]
        public int bottleneckQueue = 3;
        [Tooltip("A guest waited longer than this part of the patience = bottleneck")]
        public float bottleneckWaitFraction = 0.5f;

        [Header("Needs / buildings")]
        public NeedConfig[] needs =
        {
            new NeedConfig { need = NeedType.Rooms,     buildingName = "Guest Hotel", spriteKey = "rooms",
                             enabled = true, displayName = "Rooms", icon = "UI/Icons/icon_need_rooms", baseServiceTime = 8.0f },
            new NeedConfig { need = NeedType.Drinks,    buildingName = "Tiki Bar",    spriteKey = "bar",
                             enabled = true, displayName = "Drinks", icon = "UI/Icons/icon_need_drinks", baseServiceTime = 4.8f },
            new NeedConfig { need = NeedType.Food,      buildingName = "Restaurant",  spriteKey = "food",
                             enabled = true, displayName = "Food", icon = "UI/Icons/icon_need_food", baseServiceTime = 7.2f },
            new NeedConfig { need = NeedType.Relax,     buildingName = "Pool",        spriteKey = "pool",
                             enabled = true, displayName = "Relax", icon = "UI/Icons/icon_need_relax", baseServiceTime = 8.0f },
            new NeedConfig { need = NeedType.OceanView, buildingName = "Bungalows",   spriteKey = "bungalows",
                             enabled = true, displayName = "Ocean View", icon = "UI/Icons/icon_need_oceanview", baseServiceTime = 9.6f },
        };

        [Header("Island")]
        public IslandConfig island = new IslandConfig();

        [Header("Waves")]
        public WaveConfig[] waves = { new WaveConfig() };

        public const string ResourcePath = "IslandTycoon/GameConfig";

        public static GameConfig Load()
        {
            var cfg = Resources.Load<GameConfig>(ResourcePath);
            return cfg != null ? cfg : CreateInstance<GameConfig>();
        }

        public NeedConfig Need(NeedType t) => Need((int)t);

        public NeedConfig Need(int t)
        {
            foreach (var n in needs) if ((int)n.need == t) return n;
            string name = ((NeedType)t).ToString();
            return new NeedConfig { need = (NeedType)t, buildingName = name, spriteKey = name.ToLower(), displayName = name };
        }

        /// <summary>Config of a 0-based wave; past the end of the list the last wave repeats.</summary>
        public WaveConfig Wave(int index)
        {
            if (waves == null || waves.Length == 0) return new WaveConfig();
            return waves[Mathf.Clamp(index, 0, waves.Length - 1)];
        }

        public PlotConfig Plot(int need)
        {
            foreach (var p in island.plots) if ((int)p.need == need) return p;
            return null;
        }
    }
}
