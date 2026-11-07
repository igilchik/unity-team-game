using System;
using System.Collections.Generic;

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
        public event Action WaveStarted;

        public GameModel(GameConfig cfg)
        {
            Cfg = cfg;
            S = NewState();
        }

        GameState NewState()
        {
            var s = new GameState { coins = Cfg.startCoins, energy = Cfg.startEnergy, stats = NewStats() };
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
        public bool WaveActive => S.phase == GamePhase.WaveActive;
        /// <summary>1-based number of the next / current wave.</summary>
        public int WaveNumber => S.waveIndex + 1;
        public BuildingState B(int need) => S.buildings[need];
        public BuildingState B(NeedType need) => S.buildings[(int)need];
        public bool Exists(int need) => B(need).exists;
        public PlotConfig Plot(int need) => Cfg.Plot(need);

        /// <summary>Guests can ask for the need now: the need type is implemented and its building is open.</summary>
        public bool IsNeedActive(int need) => Cfg.Need(need).enabled && B(need).exists && B(need).unlocked;

        /// <summary>Aggregated numbers of one need in the current wave.</summary>
        public NeedStats Stats(int need) => S.stats[need];

        /// <summary>Guests waiting in the queue of a building.</summary>
        public int QueueCount(int need)
        {
            int n = 0;
            foreach (var g in S.guests) if (g.stage == GuestStage.Queued && g.CurrentNeed == need) n++;
            return n;
        }

        /// <summary>Guests being served by a building right now.</summary>
        public int InServiceCount(int need)
        {
            int n = 0;
            foreach (var g in S.guests) if (g.stage == GuestStage.InService && g.CurrentNeed == need) n++;
            return n;
        }

        /// <summary>Guests of the wave that have not arrived yet.</summary>
        public int GuestsToCome => S.plans.Count - S.nextPlan;

        /// <summary>Everybody arrived and every request was served.</summary>
        public bool AllGuestsDone => S.phase == GamePhase.WaveActive && GuestsToCome == 0 && S.guests.Count == 0;

        public int OpenBuildings
        {
            get { int n = 0; foreach (var b in S.buildings) if (b.exists && b.unlocked) n++; return n; }
        }
    

        // ------------------------------------------------------------------ guest needs

        /// <summary>
        /// Guests of a wave and their needs, from the config of that wave. Only active needs are used.
        /// Deterministic per wave, so the same wave always brings the same guests.
        /// </summary>
        public List<GuestPlan> GeneratePlans(int waveIndex)
        {
            var wc = Cfg.Wave(waveIndex);
            var rnd = new System.Random(1000 + waveIndex * 7919);
            var plans = new List<GuestPlan>();
            var active = new List<int>();
            foreach (var n in Needs.All) if (IsNeedActive((int)n)) active.Add((int)n);
            if (active.Count == 0) return plans;

            int guests = Math.Max(1, wc.guests);
            for (int i = 0; i < guests; i++)
            {
                var p = new GuestPlan { time = i * wc.spacing, skin = rnd.Next(0, 6) };
                int count = Math.Max(1, Math.Min(rnd.Next(wc.minNeeds, wc.maxNeeds + 1), active.Count));
                var pool = new List<int>(active);
                for (int k = 0; k < count; k++)
                {
                    float total = 0;
                    foreach (var n in pool) total += Weight(wc, n);
                    float r = (float)rnd.NextDouble() * total;
                    int pick = pool[pool.Count - 1];
                    foreach (var n in pool) { r -= Weight(wc, n); if (r <= 0) { pick = n; break; } }
                    p.needs.Add(pick);
                    pool.Remove(pick);
                }
                plans.Add(p);
            }
            return plans;
        }

        float Weight(WaveConfig wc, int need)
        {
            float w = wc.weights != null && need < wc.weights.Length ? wc.weights[need] : 1f;
            return Math.Max(0.01f, w * Cfg.Need(need).weight);
        }

        static List<NeedStats> NewStats()
        {
            var l = new List<NeedStats>();
            for (int i = 0; i < Needs.Count; i++) l.Add(new NeedStats());
            return l;
        }

        // ------------------------------------------------------------------ wave

        /// <summary>A wave can be started only while the resort is in AWAITING_WAVE.</summary>
        public bool CanStartWave => S.phase == GamePhase.AwaitingWave;

        /// <summary>Moves the resort from AWAITING_WAVE into an active wave. Refused in any other state.</summary>
        public bool StartWave()
        {
            if (!CanStartWave) return false;
            S.plans = GeneratePlans(S.waveIndex);
            S.stats = NewStats();
            foreach (var p in S.plans) foreach (var n in p.needs) S.stats[n].requested++;
            S.guests.Clear();
            S.nextPlan = 0;
            S.waveTime = 0;
            S.phase = GamePhase.WaveActive;
            WaveStarted?.Invoke();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Advances the active wave by dt seconds: guests arrive, walk to the building of their need,
        /// queue, get served (first come, first served) and go on to their next need or leave.
        /// </summary>
        public void Tick(float dt)
        {
            if (S.phase != GamePhase.WaveActive || dt <= 0) return;
            dt = Math.Min(dt, 0.25f);
            S.waveTime += dt;

            // arrivals
            while (S.nextPlan < S.plans.Count && S.plans[S.nextPlan].time <= S.waveTime)
            {
                var p = S.plans[S.nextPlan++];
                S.guests.Add(new Guest
                {
                    id = S.nextGuestId++, skin = p.skin, needs = new List<int>(p.needs),
                    stage = GuestStage.Walking, timer = Cfg.walkTime,
                });
            }

            // guests
            foreach (var g in S.guests)
            {
                switch (g.stage)
                {
                    case GuestStage.Walking:
                        g.timer -= dt;
                        if (g.timer <= 0) { g.stage = GuestStage.Queued; g.waited = 0; }
                        break;
                    case GuestStage.Queued:
                        g.waited += dt;
                        break;
                    case GuestStage.InService:
                        g.progress += dt / Math.Max(0.1f, Cfg.Need(g.CurrentNeed).baseServiceTime);
                        if (g.progress >= 1f) Serve(g);
                        break;
                    case GuestStage.Leaving:
                        g.timer -= dt;
                        if (g.timer <= 0) g.stage = GuestStage.Done;
                        break;
                }
            }
            S.guests.RemoveAll(g => g.stage == GuestStage.Done);

            // queues -> free service slots, first come first served
            foreach (var b in S.buildings)
            {
                if (!b.unlocked) continue;
                int free = Math.Max(1, Cfg.serviceSlots) - InServiceCount(b.need);
                foreach (var g in S.guests)
                {
                    if (free <= 0) break;
                    if (g.stage != GuestStage.Queued || g.CurrentNeed != b.need) continue;
                    g.stage = GuestStage.InService;
                    g.progress = 0;
                    free--;
                }
            }
            Changed?.Invoke();
        }

        void Serve(Guest g)
        {
            S.stats[g.CurrentNeed].served++;
            g.index++;
            g.progress = 0;
            g.waited = 0;
            g.timer = Cfg.walkTime;
            g.stage = g.index < g.needs.Count ? GuestStage.Walking : GuestStage.Leaving;
        }
    }
}
