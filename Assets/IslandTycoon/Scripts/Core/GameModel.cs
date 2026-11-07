using System;

namespace IslandTycoon
{
    /// <summary>
    /// Pure game logic. No rendering, no MonoBehaviour: the view only reads state
    /// and calls the public commands, so every rule is testable.
    /// </summary>
    public class GameModel
    {
        public readonly GameConfig Cfg;
        public GameState S { get; private set; }

        /// <summary>Raised after every change of the game state; the UI refreshes from it.</summary>
        public event Action Changed;

        public GameModel(GameConfig cfg)
        {
            Cfg = cfg;
            S = NewState();
        }

        GameState NewState()
        {
            var s = new GameState { coins = Cfg.startCoins, energy = Cfg.startEnergy };
            foreach (var n in Needs.All)
            {
                var plot = Cfg.Plot((int)n);
                s.buildings.Add(new BuildingState
                {
                    need = (int)n,
                    exists = plot != null,
                    unlocked = plot != null && plot.unlockAfterWave == 0,
                });
            }
            return s;
        }

        /// <summary>Tells the views that the state changed (also after direct edits of S).</summary>
        public void NotifyChanged() => Changed?.Invoke();

        // ------------------------------------------------------------------ queries

        public IslandConfig IC => Cfg.island;
        public GamePhase Phase => S.phase;
        public bool AwaitingWave => S.phase == GamePhase.AwaitingWave;
        /// <summary>1-based number of the next / current wave.</summary>
        public int WaveNumber => S.waveIndex + 1;
        public BuildingState B(int need) => S.buildings[need];
        public BuildingState B(NeedType need) => S.buildings[(int)need];
        public bool Exists(int need) => B(need).exists;
        public PlotConfig Plot(int need) => Cfg.Plot(need);

        public int OpenBuildings
        {
            get { int n = 0; foreach (var b in S.buildings) if (b.exists && b.unlocked) n++; return n; }
        }
    }
}
