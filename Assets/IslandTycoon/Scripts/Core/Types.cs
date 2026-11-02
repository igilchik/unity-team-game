using System;
using System.Collections.Generic;

namespace IslandTycoon
{
    /// <summary>Guest need = service type. One building serves exactly one need.</summary>
    public enum NeedType { Rooms = 0, Drinks = 1, Food = 2, Relax = 3, OceanView = 4 }

    public static class Needs
    {
        public const int Count = 5;
        public static readonly NeedType[] All =
            { NeedType.Rooms, NeedType.Drinks, NeedType.Food, NeedType.Relax, NeedType.OceanView };
    }

    /// <summary>
    /// Resort state machine. AWAITING_WAVE: the player prepares the resort before the next wave.
    /// WAVE_ACTIVE: guests are coming and the resort serves them.
    /// </summary>
    public enum GamePhase { AwaitingWave = 0, WaveActive = 1 }

    [Serializable]
    public class BuildingState
    {
        public int need;
        public bool exists;      // the island has a plot for this need
        public bool unlocked;    // the building is open (locked areas open later)
    }

    /// <summary>Whole state of the resort.</summary>
    [Serializable]
    public class GameState
    {
        public int coins;
        public int energy;
        public int resortValue;

        public GamePhase phase = GamePhase.AwaitingWave;
        public int waveIndex;        // 0-based index of the next / current wave

        // active wave
        public float waveTime;       // seconds since the wave started
        public List<BuildingState> buildings = new List<BuildingState>();
    }
}
