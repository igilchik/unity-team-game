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
        /// <summary>No more new guests / requests in this wave.</summary>
        public event Action ArrivalsClosed;
        /// <summary>The wave is over: no new guests and every queue is empty.</summary>
        public event Action WaveEnded;
        /// <summary>Workers of a building changed (need).</summary>
        public event Action<int> WorkersMoved;

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

            // start workers: one by one to the open buildings, the rest stays free
            s.totalWorkers = Math.Max(0, Cfg.island.startWorkers);
            int w = s.totalWorkers;
            while (w > 0)
            {
                bool any = false;
                foreach (var b in s.buildings) if (b.unlocked && w > 0) { b.workers++; w--; any = true; }
                if (!any) break;
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
        public bool WaveFinishing => S.phase == GamePhase.WaveFinishing;
        /// <summary>A wave is running: guests arrive (WAVE_ACTIVE) or the last ones are served (WAVE_FINISHING).</summary>
        public bool InWave => S.phase == GamePhase.WaveActive || S.phase == GamePhase.WaveFinishing;
        public bool WaveComplete => S.phase == GamePhase.WaveComplete;
        /// <summary>1-based number of the next / current wave.</summary>
        public int WaveNumber => S.waveIndex + 1;
        public BuildingState B(int need) => S.buildings[need];
        public BuildingState B(NeedType need) => S.buildings[(int)need];
        public bool Exists(int need) => B(need).exists;
        public PlotConfig Plot(int need) => Cfg.Plot(need);

        /// <summary>Guests can ask for the need now: the need type is implemented and its building is open.</summary>
        public bool IsNeedActive(int need) => Cfg.Need(need).enabled && B(need).exists && B(need).unlocked;

        /// <summary>Workers working in some building.</summary>
        public int AssignedWorkers { get { int n = 0; foreach (var b in S.buildings) n += b.workers; return n; } }

        /// <summary>Workers waiting for a job (standing by the pier).</summary>
        public int FreeWorkers => S.totalWorkers - AssignedWorkers;

        public int Workers(int need) => B(need).workers;

        /// <summary>An open building with nobody working in it (e.g. after its worker was moved away).</summary>
        public bool IsUnstaffed(int need) => B(need).exists && B(need).unlocked && B(need).workers == 0;

        /// <summary>Open buildings without a worker.</summary>
        public int UnstaffedBuildings
        {
            get { int n = 0; for (int i = 0; i < S.buildings.Count; i++) if (IsUnstaffed(i)) n++; return n; }
        }

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

        /// <summary>Longest wait in the queue of a building as a part of the guest patience (0..1+).</summary>
        public float LongestWaitFraction(int need)
        {
            float w = 0;
            foreach (var g in S.guests)
                if (g.stage == GuestStage.Queued && g.CurrentNeed == need) w = Math.Max(w, g.waited / Math.Max(0.1f, Cfg.patience));
            return w;
        }

        /// <summary>
        /// Bottleneck: during a wave the queue at the building is too long or a guest
        /// has been waiting too long. The player should send a worker there.
        /// </summary>
        public bool IsBottleneck(int need) =>
            InWave && IsNeedActive(need) &&
            (QueueCount(need) >= Cfg.bottleneckQueue || LongestWaitFraction(need) > Cfg.bottleneckWaitFraction);

        // ------------------------------------------------------------------ satisfaction

        /// <summary>
        /// Score of one request served after waiting <paramref name="waited"/> seconds in the queue:
        /// 1 when served fast, falling to lateScore when the guest almost ran out of patience.
        /// </summary>
        public float RequestScore(float waited)
        {
            float pat = Math.Max(0.1f, Cfg.patience);
            float fast = pat * Cfg.fastServeFraction;
            if (waited <= fast) return 1f;
            float t = Math.Min(1f, (waited - fast) / Math.Max(0.01f, pat - fast));
            return 1f + (Cfg.lateScore - 1f) * t;
        }

        /// <summary>
        /// Wave Satisfaction 0..100: average score of the requests resolved so far (100 when none yet).
        /// Served requests count with their score, failed ones (guest lost patience) with 0.
        /// </summary>
        public float WaveSatisfaction()
        {
            float score = 0; int resolved = 0;
            foreach (var st in S.stats) { score += st.score; resolved += st.Resolved; }
            return resolved == 0 ? 100f : 100f * score / resolved;
        }

        /// <summary>
        /// Live Wave Satisfaction 0..100 during the wave: served requests plus the guests waiting
        /// right now, scored by how long they have waited so far. It drops while queues grow.
        /// </summary>
        public float LiveSatisfaction()
        {
            float score = 0; int count = 0;
            foreach (var st in S.stats) { score += st.score; count += st.Resolved; }
            foreach (var g in S.guests)
            {
                if (g.stage != GuestStage.Queued && g.stage != GuestStage.InService) continue;
                score += RequestScore(g.waited);
                count++;
            }
            return count == 0 ? 100f : 100f * score / count;
        }

        /// <summary>Guests of the wave that have not arrived yet (0 once the arrivals are closed).</summary>
        public int GuestsToCome => S.arrivalsOpen ? S.plans.Count - S.nextPlan : 0;

        /// <summary>
        /// Requests of one need still waiting to be resolved in this wave: guests on the way,
        /// in the queue or in service, their later needs, and guests that have not arrived yet.
        /// </summary>
        public int RemainingRequests(int need)
        {
            int n = 0;
            foreach (var g in S.guests)
            {
                if (g.stage == GuestStage.Leaving || g.stage == GuestStage.Done) continue;
                for (int k = g.index; k < g.needs.Count; k++) if (g.needs[k] == need) n++;
            }
            if (S.arrivalsOpen)
                for (int i = S.nextPlan; i < S.plans.Count; i++)
                    foreach (var x in S.plans[i].needs) if (x == need) n++;
            return n;
        }

        /// <summary>All requests of the wave still waiting to be resolved.</summary>
        public int RemainingRequests()
        {
            int n = 0;
            for (int i = 0; i < Needs.Count; i++) n += RemainingRequests(i);
            return n;
        }

        /// <summary>Requests standing in the queues right now (not yet in service).</summary>
        public int QueuedRequests()
        {
            int n = 0;
            for (int i = 0; i < Needs.Count; i++) n += QueueCount(i);
            return n;
        }

        /// <summary>New guests can still arrive in this wave.</summary>
        public bool ArrivalsOpen => S.arrivalsOpen;

        /// <summary>Everybody arrived and every request was served.</summary>
        public bool AllGuestsDone => (InWave || WaveComplete) && GuestsToCome == 0 && RemainingRequests() == 0;

        public int OpenBuildings
        {
            get { int n = 0; foreach (var b in S.buildings) if (b.exists && b.unlocked) n++; return n; }
        }
    

        // ------------------------------------------------------------------ workers

        /// <summary>
        /// Workers can be moved while the player prepares the resort (AWAITING_WAVE)
        /// and also during an active wave, to react to bottlenecks. Moves are instant and free.
        /// </summary>
        public bool CanMoveWorkers => S.phase == GamePhase.AwaitingWave || InWave;

        /// <summary>Coins one worker move costs. 0 by design: moving workers is free.</summary>
        public int MoveCost => Math.Max(0, Cfg.workerMoveCost);

        /// <summary>The player has the coins for one move.</summary>
        public bool CanAffordMove => S.coins >= MoveCost;

        /// <summary>Pays for one move (nothing with the default cost of 0).</summary>
        bool PayMove()
        {
            if (!CanAffordMove) return false;
            S.coins -= MoveCost;
            return true;
        }

        /// <summary>Puts a free worker into an open building.</summary>
        public bool AssignWorker(int need) => MoveWorker(need, +1);

        /// <summary>Takes a worker out of a building; he becomes free.</summary>
        public bool UnassignWorker(int need) => MoveWorker(need, -1);

        /// <summary>
        /// delta &gt; 0: a free worker goes to the building, delta &lt; 0: a worker leaves it.
        /// One worker per call; locked buildings take no workers.
        /// </summary>
        public bool MoveWorker(int need, int delta)
        {
            if (!CanMoveWorkers) return false;
            if (need < 0 || need >= S.buildings.Count || delta == 0) return false;
            var b = B(need);
            if (!b.exists || !b.unlocked) return false;
            if (delta > 0 && FreeWorkers <= 0) return false;
            if (delta < 0 && b.workers <= 0) return false;
            if (!PayMove()) return false;
            b.workers += delta > 0 ? 1 : -1;
            WorkersMoved?.Invoke(need);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// + in the middle of a wave: a free worker if there is one, otherwise the worker
        /// is taken from the building with the most workers.
        /// </summary>
        public bool PullWorker(int need)
        {
            if (!CanMoveWorkers || need < 0 || need >= S.buildings.Count || !B(need).unlocked) return false;
            if (FreeWorkers > 0) return MoveWorker(need, +1);
            int donor = -1, most = 0;
            foreach (var b in S.buildings)
                if (b.need != need && b.workers > most) { most = b.workers; donor = b.need; }
            return donor >= 0 && TransferWorker(donor, need);
        }

        /// <summary>
        /// Moves one worker straight from one building to another, instantly: no walking time,
        /// the new building has the worker in the same moment. Allowed before and during a wave.
        /// </summary>
        public bool TransferWorker(int from, int to)
        {
            if (!CanTransfer(from, to) || !PayMove()) return false;
            B(from).workers--;
            B(to).workers++;
            WorkersMoved?.Invoke(from);
            WorkersMoved?.Invoke(to);
            Changed?.Invoke();
            return true;
        }

        /// <summary>A worker of building <paramref name="from"/> can go to building <paramref name="to"/> now.</summary>
        public bool CanTransfer(int from, int to)
        {
            if (!CanMoveWorkers || !CanAffordMove || from == to) return false;
            if (from < 0 || to < 0 || from >= S.buildings.Count || to >= S.buildings.Count) return false;
            var a = B(from); var b = B(to);
            return a.exists && b.exists && b.unlocked && a.workers > 0;
        }

        /// <summary>Some worker could come to the building now (free or from another building).</summary>
        public bool CanPullWorker(int need) =>
            CanMoveWorkers && CanAffordMove && B(need).unlocked && (FreeWorkers > 0 || AssignedWorkers - B(need).workers > 0);

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
            S.arrivalsOpen = true;
            S.waveTime = 0;
            S.satisfaction = 100f;
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
            if (!InWave || dt <= 0) return;
            dt = Math.Min(dt, 0.25f);
            S.waveTime += dt;

            // arrivals
            while (S.arrivalsOpen && S.nextPlan < S.plans.Count && S.plans[S.nextPlan].time <= S.waveTime)
            {
                var p = S.plans[S.nextPlan++];
                S.guests.Add(new Guest
                {
                    id = S.nextGuestId++, skin = p.skin, needs = new List<int>(p.needs),
                    stage = GuestStage.Walking, timer = Cfg.walkTime,
                });
            }
            float duration = Cfg.Wave(S.waveIndex).duration;
            if (S.arrivalsOpen && (S.nextPlan >= S.plans.Count || (duration > 0 && S.waveTime >= duration)))
                CloseArrivals();

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
                        if (g.waited > Cfg.patience) Fail(g);
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
            S.satisfaction = LiveSatisfaction();

            // the wave ends only when no new guests come and every queue is empty
            if (S.phase == GamePhase.WaveFinishing && RemainingRequests() == 0) EndWave();
            Changed?.Invoke();
        }

        /// <summary>WAVE_FINISHING -> WAVE_COMPLETE: the final Wave Satisfaction is fixed.</summary>
        void EndWave()
        {
            S.guests.Clear();   // the last guests walk off the island
            S.satisfaction = WaveSatisfaction();
            S.phase = GamePhase.WaveComplete;
            WaveEnded?.Invoke();
        }

        /// <summary>
        /// Stops new requests: no more guests arrive in this wave. Guests that have not arrived yet
        /// are dropped together with their requests; guests already in the resort are still served.
        /// </summary>
        public void CloseArrivals()
        {
            if (!S.arrivalsOpen) return;
            for (int i = S.nextPlan; i < S.plans.Count; i++)
                foreach (var n in S.plans[i].needs) S.stats[n].requested--;
            if (S.nextPlan < S.plans.Count) S.plans.RemoveRange(S.nextPlan, S.plans.Count - S.nextPlan);
            S.arrivalsOpen = false;
            // the wave goes on until the guests already in the resort are served
            if (S.phase == GamePhase.WaveActive) S.phase = GamePhase.WaveFinishing;
            ArrivalsClosed?.Invoke();
            Changed?.Invoke();
        }

        void Serve(Guest g)
        {
            var st = S.stats[g.CurrentNeed];
            st.served++;
            st.score += RequestScore(g.waited);
            g.index++;
            g.progress = 0;
            g.waited = 0;
            g.timer = Cfg.walkTime;
            g.stage = g.index < g.needs.Count ? GuestStage.Walking : GuestStage.Leaving;
        }

        /// <summary>
        /// The guest waited longer than the patience: the request fails (score 0) and the guest
        /// gives up the remaining needs as well and leaves the resort.
        /// </summary>
        void Fail(Guest g)
        {
            S.stats[g.CurrentNeed].failed++;
            for (int k = g.index + 1; k < g.needs.Count; k++) S.stats[g.needs[k]].failed++;
            g.index = g.needs.Count;
            g.progress = 0;
            g.timer = Cfg.walkTime;
            g.stage = GuestStage.Leaving;
        }
    }
}
