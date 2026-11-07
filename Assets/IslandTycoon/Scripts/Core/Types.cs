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
        public List<BuildingState> buildings = new List<BuildingState>();
    }
}
