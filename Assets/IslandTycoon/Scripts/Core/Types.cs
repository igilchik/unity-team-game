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
        public int workers;      // workers assigned to this building
    }

    /// <summary>Where a guest is on the way through the resort.</summary>
    public enum GuestStage { Walking = 0, Queued = 1, InService = 2, Leaving = 3, Done = 4 }

    /// <summary>A guest in the resort during an active wave.</summary>
    [Serializable]
    public class Guest
    {
        public int id;
        public int skin;
        public List<int> needs = new List<int>();
        public int index;          // which need of the list is current
        public GuestStage stage;
        public float timer;        // walking / leaving countdown
        public float waited;       // time spent in the queue for the current need
        public float progress;     // service progress 0..1
        public int CurrentNeed => index < needs.Count ? needs[index] : -1;
    }

    /// <summary>One guest of a wave, generated before the wave starts.</summary>
    [Serializable]
    public class GuestPlan
    {
        public float time;           // arrival, seconds after the wave start
        public int skin;             // which guest picture
        public List<int> needs = new List<int>();   // NeedType values, in order
    }

    /// <summary>Per-need numbers of the current wave.</summary>
    [Serializable]
    public class NeedStats
    {
        public int requested;   // requests for this need in the wave
        public int served;      // requests already served
        public float score;     // sum of the satisfaction scores of served requests (0..1 each)
        public int Remaining => requested - served;
    }

    /// <summary>Whole state of the resort.</summary>
    [Serializable]
    public class GameState
    {
        public int coins;
        public int energy;
        public int resortValue;

        public int totalWorkers;     // all workers of the resort (assigned + free)

        public GamePhase phase = GamePhase.AwaitingWave;
        public int waveIndex;        // 0-based index of the next / current wave

        // active wave
        public float waveTime;       // seconds since the wave started
        public List<GuestPlan> plans = new List<GuestPlan>();
        public int nextPlan;         // next guest to arrive
        public List<Guest> guests = new List<Guest>();
        public int nextGuestId;
        public List<NeedStats> stats = new List<NeedStats>();
        public List<BuildingState> buildings = new List<BuildingState>();
    }
}
