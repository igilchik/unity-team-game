using System;
using System.Text;
using UnityEngine;

namespace IslandTycoon
{
    /// <summary>
    /// Headless checks of the game rules and the UI bindings.
    /// Run from the menu Island Tycoon > Run Self Test (result goes to the Console).
    /// Works in Edit mode and in Play mode.
    /// </summary>
    public static class SelfTest
    {
        static StringBuilder _sb;
        static int _fails;

        public static bool Passed => _fails == 0;

        public static string Run()
        {
            _sb = new StringBuilder();
            _fails = 0;
            var cfg = GameConfig.Load();

            Section("SCRUM-22 Home view data", () => HomeData(cfg));
            Section("SCRUM-23 AWAITING_WAVE", () => AwaitingWave(cfg));
            Section("SCRUM-27 Start wave", () => StartWave(cfg));
            Section("SCRUM-40 Guest needs generation", () => NeedsGeneration(cfg));
            Section("SCRUM-47 Wave Needs Panel during a wave", () => NeedsPanelDuringWave(cfg));
            Section("SCRUM-79 Moving workers between buildings", () => WorkerMoves(cfg));
            Section("SCRUM-53 Satisfaction and bottlenecks", () => SatisfactionBottleneck(cfg));
            Section("SCRUM-59 Wave end with a backlog", () => WaveEndBacklog());
            Section("SCRUM-86 Moving workers costs no currency", () => FreeMoves(cfg));
            Section("SCRUM-92 Building performance with and without a worker", () => WorkerPerformance());
            Section("SCRUM-100 Wave Complete screen shows the finished wave", () => WaveCompleteScreen());
            Section("SCRUM-108 Resource update and Claim Rewards", () => ResourceUpdate());

            _sb.Insert(0, _fails == 0 ? "ALL CHECKS PASSED\n" : $"{_fails} CHECK(S) FAILED\n");
            return _sb.ToString();
        }

        static void Check(bool ok, string what)
        {
            if (!ok) _fails++;
            _sb.AppendLine((ok ? "PASS " : "FAIL ") + what);
        }

        static void Section(string name, Action body)
        {
            _sb.AppendLine("== " + name);
            try { body(); }
            catch (Exception e) { Check(false, $"{name} threw {e.GetType().Name}: {e.Message}"); }
        }

        static void Dispose(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        // ------------------------------------------------------------------ SCRUM-22

        /// <summary>The Home view shows the right values and follows changes of the data.</summary>
        static void HomeData(GameConfig cfg)
        {
            var m = new GameModel(cfg);
            Check(m.S.coins == cfg.startCoins, "start Coins come from the config");
            Check(m.S.energy == cfg.startEnergy, "start Energy comes from the config");
            Check(m.S.resortValue == 0, "Resort Value starts at 0");
            Check(m.B(NeedType.Rooms).unlocked && m.B(NeedType.Drinks).unlocked && m.B(NeedType.Food).unlocked,
                "Hotel, Bar and Restaurant are open at start");
            Check(!m.B(NeedType.Relax).unlocked && !m.B(NeedType.OceanView).unlocked, "Pool and Bungalows are locked at start");
            Check(m.OpenBuildings == 3, "3 buildings open");

            var go = new GameObject("SelfTest_Hud");
            try
            {
                var hud = go.AddComponent<HudView>();
                hud.Build(m);
                Check(hud.CoinsText.text == cfg.startCoins.ToString(), "HUD shows start Coins");
                Check(hud.EnergyText.text == cfg.startEnergy.ToString(), "HUD shows start Energy");
                Check(hud.ValueText.text == "0", "HUD shows Resort Value 0");
                Check(hud.StatusText.text.Contains(cfg.island.name), "HUD shows the island name");
                Check(hud.StatusText.text.Contains("3 of 5"), "HUD shows open buildings");

                m.S.coins += 25;
                m.S.energy -= 1;
                m.S.resortValue = 40;
                m.NotifyChanged();
                Check(hud.CoinsText.text == (cfg.startCoins + 25).ToString(), "HUD follows Coins changes");
                Check(hud.EnergyText.text == (cfg.startEnergy - 1).ToString(), "HUD follows Energy changes");
                Check(hud.ValueText.text == "40", "HUD follows Resort Value changes");
            }
            finally { Dispose(go); }
        }

        // ------------------------------------------------------------------ SCRUM-23

        static void AwaitingWave(GameConfig cfg)
        {
            var m = new GameModel(cfg);
            Check(m.Phase == GamePhase.AwaitingWave && m.AwaitingWave, "new game starts in AWAITING_WAVE");
            Check(m.WaveNumber == 1, "first wave is wave 1");
            Check(m.CanStartWave, "Start Wave allowed in AWAITING_WAVE");
        }

        // ------------------------------------------------------------------ SCRUM-27

        static void StartWave(GameConfig cfg)
        {
            var m = new GameModel(cfg);
            int started = 0;
            m.WaveStarted += () => started++;
            Check(m.StartWave(), "StartWave from AWAITING_WAVE");
            Check(m.Phase == GamePhase.WaveActive, "AWAITING_WAVE -> WAVE_ACTIVE");
            Check(started == 1, "WaveStarted raised once");
            Check(!m.CanStartWave && !m.StartWave(), "StartWave refused during WAVE_ACTIVE");
            Check(started == 1 && m.Phase == GamePhase.WaveActive, "second start changes nothing");
            m.Tick(0.1f);
            m.Tick(0.1f);
            Check(Mathf.Abs(m.S.waveTime - 0.2f) < 0.001f, "wave time runs during the wave");
            var idle = new GameModel(cfg);
            idle.Tick(1f);
            Check(idle.S.waveTime == 0 && idle.Phase == GamePhase.AwaitingWave, "nothing runs before the wave");

            // the button follows the state
            var m2 = new GameModel(cfg);
            int pressed = 0;
            var go = new GameObject("SelfTest_StartWave");
            try
            {
                var hud = go.AddComponent<HudView>();
                hud.Build(m2, () => { pressed++; m2.StartWave(); });
                Check(hud.StartWaveButton.gameObject.activeSelf, "START WAVE visible in AWAITING_WAVE");
                hud.StartWaveButton.onClick.Invoke();
                Check(pressed == 1 && m2.Phase == GamePhase.WaveActive, "START WAVE starts the wave");
                Check(!hud.StartWaveButton.gameObject.activeSelf, "START WAVE hidden during the wave");
                Check(hud.StatusText.text.Contains("in progress"), "HUD shows the running wave");
            }
            finally { Dispose(go); }
        }
    

        // ------------------------------------------------------------------ SCRUM-40

        static void NeedsGeneration(GameConfig cfg)
        {
            var m = new GameModel(cfg);
            var wc = cfg.Wave(0);
            var plans = m.GeneratePlans(0);
            Check(plans.Count == wc.guests, $"wave 1 brings {wc.guests} guests");

            bool countsOk = true, unique = true, onlyActive = true, ordered = true;
            int total = 0;
            for (int i = 0; i < plans.Count; i++)
            {
                var p = plans[i];
                countsOk &= p.needs.Count >= Mathf.Max(1, wc.minNeeds) && p.needs.Count <= wc.maxNeeds;
                for (int a = 0; a < p.needs.Count; a++)
                {
                    onlyActive &= m.IsNeedActive(p.needs[a]);
                    for (int b = a + 1; b < p.needs.Count; b++) unique &= p.needs[a] != p.needs[b];
                }
                if (i > 0) ordered &= p.time > plans[i - 1].time;
                total += p.needs.Count;
            }
            Check(countsOk, "needs per guest within the wave config");
            Check(unique, "a guest never asks twice for the same need");
            Check(onlyActive, "only needs of open buildings are generated (no Relax / Ocean View yet)");
            Check(ordered, "guests arrive one after another");

            var again = new GameModel(cfg).GeneratePlans(0);
            bool same = again.Count == plans.Count;
            for (int i = 0; same && i < plans.Count; i++)
                same = again[i].needs.Count == plans[i].needs.Count && again[i].needs[0] == plans[i].needs[0];
            Check(same, "the same wave always brings the same guests");

            m.StartWave();
            int requested = 0;
            foreach (var st in m.S.stats) requested += st.requested;
            Check(m.S.plans.Count == plans.Count && requested == total, "StartWave counts every request");

            // weights and the prepared needs follow the config
            var custom = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                custom.waves = new[] { new WaveConfig { guests = 20, minNeeds = 2, maxNeeds = 2, weights = new float[] { 0, 0, 0, 1, 0 } } };
                var mc = new GameModel(custom);
                Check(Has(mc.GeneratePlans(0), NeedType.Relax) == false, "Relax is not asked for while the Pool is locked");
                mc.B(NeedType.Relax).unlocked = true;
                var withPool = mc.GeneratePlans(0);
                Check(Has(withPool, NeedType.Relax), "Relax is asked for once the Pool is open");
                Check(withPool[0].needs.Count == 2, "2 needs per guest when the wave says so");

                custom.Need(NeedType.Drinks).enabled = false;
                Check(!Has(mc.GeneratePlans(0), NeedType.Drinks), "a disabled need is never generated");
            }
            finally { Dispose(custom); }
        }

        static bool Has(System.Collections.Generic.List<GuestPlan> plans, NeedType need)
        {
            foreach (var p in plans) if (p.needs.Contains((int)need)) return true;
            return false;
        }
    

        // ------------------------------------------------------------------ SCRUM-47

        static NeedsPanel MakePanel(GameModel m, GameObject go)
        {
            var canvas = UIFactory.Canvas("SelfTestCanvas");
            canvas.transform.SetParent(go.transform, false);
            var panel = go.AddComponent<NeedsPanel>();
            panel.Build(m, canvas.transform);
            return panel;
        }

        static void NeedsPanelDuringWave(GameConfig cfg)
        {
            var m = new GameModel(cfg);
            var go = new GameObject("SelfTest_Needs");
            try
            {
                var panel = MakePanel(m, go);
                int rooms = (int)NeedType.Rooms;
                Check(panel.Header.text == "GUEST NEEDS", "panel header before the wave");
                Check(panel.RowVisible(rooms) && panel.RowVisible((int)NeedType.Drinks) && panel.RowVisible((int)NeedType.Food),
                    "rows for Rooms, Drinks and Food");
                Check(!panel.RowVisible((int)NeedType.Relax) && !panel.RowVisible((int)NeedType.OceanView), "no rows for locked needs");
                Check(panel.RowValue(rooms).Contains("no guests"), "no values before the wave");

                m.StartWave();
                Check(panel.Header.text == "WAVE 1 NEEDS", "panel header during the wave");
                int busiest = rooms;
                for (int n = 0; n < Needs.Count; n++) if (m.Stats(n).requested > m.Stats(busiest).requested) busiest = n;
                var st = m.Stats(busiest);
                Check(panel.RowValue(busiest).Contains($"0/{st.requested}"), "requests shown when the wave starts");

                int guard = 0;
                while (st.served == 0 && guard++ < 10000) m.Tick(0.1f);
                Check(st.served > 0 && panel.RowValue(busiest).Contains($"{st.served}/{st.requested}"), "served count follows the wave");

                guard = 0;
                bool queueShown = true;
                while (!m.AllGuestsDone && guard++ < 100000)
                {
                    m.Tick(0.1f);
                    for (int n = 0; n < Needs.Count; n++)
                        if (m.IsNeedActive(n)) queueShown &= panel.RowValue(n).Contains($"{m.QueueCount(n)} wait");
                }
                Check(m.AllGuestsDone, "every guest of the wave is resolved");
                Check(queueShown, "waiting guests are always shown");
                bool allResolved = true;
                for (int n = 0; n < Needs.Count; n++)
                    if (m.IsNeedActive(n)) allResolved &= m.Stats(n).Resolved == m.Stats(n).requested
                                                       && panel.RowValue(n).Contains($"{m.Stats(n).served}/{m.Stats(n).requested}");
                Check(allResolved, "panel ends with every request served or lost");
            }
            finally { Dispose(go); }

            // bottleneck: many guests for one building at once
            var rush = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            var go2 = new GameObject("SelfTest_Bottleneck");
            try
            {
                rush.waves = new[] { new WaveConfig { guests = 8, spacing = 0.1f, weights = new float[] { 1, 0, 0, 0, 0 } } };
                var mb = new GameModel(rush);
                var panel = MakePanel(mb, go2);
                int rooms = (int)NeedType.Rooms;
                Check(!mb.IsBottleneck(rooms), "no bottleneck before the wave");
                mb.StartWave();
                for (int i = 0; i < 30; i++) mb.Tick(0.1f);
                Check(mb.QueueCount(rooms) >= rush.bottleneckQueue, "queue builds up at the Hotel");
                Check(mb.IsBottleneck(rooms), "long queue = bottleneck");
                Check(panel.RowWarning(rooms), "panel marks the bottleneck");
                Check(!mb.IsBottleneck((int)NeedType.Drinks) && !panel.RowWarning((int)NeedType.Drinks), "other needs are fine");
            }
            finally { Dispose(go2); Dispose(rush); }
        }
    

        // ------------------------------------------------------------------ SCRUM-79

        static void WorkerMoves(GameConfig cfg)
        {
            int rooms = (int)NeedType.Rooms, drinks = (int)NeedType.Drinks, food = (int)NeedType.Food, relax = (int)NeedType.Relax;
            var m = new GameModel(cfg);
            Check(m.S.totalWorkers == cfg.island.startWorkers, "resort starts with the configured workers");
            Check(m.Workers(rooms) == 1 && m.Workers(drinks) == 1 && m.Workers(food) == 1 && m.FreeWorkers == 0,
                "one worker in every open building");
            Check(m.Workers(relax) == 0, "no workers in a locked building");

            int moves = 0;
            m.WorkersMoved += n => moves++;
            int coins = m.S.coins;
            Check(!m.AssignWorker(drinks), "no free worker = cannot assign");
            Check(m.UnassignWorker(rooms) && m.FreeWorkers == 1 && m.Workers(rooms) == 0, "worker leaves the Hotel and is free");
            Check(m.AssignWorker(drinks) && m.Workers(drinks) == 2 && m.FreeWorkers == 0, "free worker goes to the Bar");
            Check(!m.UnassignWorker(rooms), "cannot take a worker from an empty building");
            Check(m.UnassignWorker(food) && !m.AssignWorker(relax) && m.FreeWorkers == 1, "locked Pool takes no workers");
            m.AssignWorker(food);
            Check(m.AssignedWorkers + m.FreeWorkers == m.S.totalWorkers, "no worker is lost or created");
            Check(moves == 4 && m.S.coins == coins, "every move is reported and costs nothing");

            // during the wave
            m.StartWave();
            Check(m.CanMoveWorkers, "workers can be moved during the wave");
            Check(m.PullWorker(rooms) && m.Workers(rooms) == 1 && m.Workers(drinks) == 1,
                "+ without free workers takes one from the busiest building");
            Check(m.UnassignWorker(food) && m.AssignWorker(drinks) && m.Workers(drinks) == 2, "moves are instant during the wave");
            Check(m.AssignedWorkers + m.FreeWorkers == m.S.totalWorkers, "still no worker lost");

            // the UI follows the moves
            var m2 = new GameModel(cfg);
            int selected = drinks;
            var go = new GameObject("SelfTest_Workers");
            try
            {
                var hud = go.AddComponent<HudView>();
                hud.Build(m2);
                var panel = go.AddComponent<WorkerPanel>();
                panel.Build(m2, hud.Root, () => selected, d => { if (d > 0) m2.PullWorker(selected); else m2.MoveWorker(selected, -1); }, () => selected = -1);
                panel.Refresh();
                Check(panel.Panel.gameObject.activeSelf && panel.WorkersText.text.EndsWith("1"), "panel shows the Bar with 1 worker");
                Check(hud.FreeWorkersText.text.EndsWith("0"), "HUD shows 0 free workers");
                panel.Minus.onClick.Invoke();
                Check(panel.WorkersText.text.EndsWith("0") && panel.FreeText.text.Contains("1"), "- updates the panel");
                Check(hud.FreeWorkersText.text.EndsWith("1"), "- updates the free workers in the HUD");
                panel.Plus.onClick.Invoke();
                Check(panel.WorkersText.text.EndsWith("1") && hud.FreeWorkersText.text.EndsWith("0"), "+ updates panel and HUD");

                selected = relax;
                panel.Refresh();
                Check(!panel.Minus.gameObject.activeSelf && !panel.Plus.gameObject.activeSelf && panel.InfoText.text.Contains("wave"),
                    "locked building shows when it opens, no - / +");
                selected = -1;
                panel.Refresh();
                Check(!panel.Panel.gameObject.activeSelf, "panel closes when nothing is selected");
            }
            finally { Dispose(go); }
        }
    

        // ------------------------------------------------------------------ SCRUM-53

        static void SatisfactionBottleneck(GameConfig cfg)
        {
            var m0 = new GameModel(cfg);
            Check(Mathf.Approximately(m0.RequestScore(0), 1f), "fast service = full score");
            Check(Mathf.Abs(m0.RequestScore(cfg.patience) - cfg.lateScore) < 0.001f, "late service = lateScore");
            Check(m0.RequestScore(cfg.patience * 0.8f) < 1f && m0.RequestScore(cfg.patience * 0.8f) > cfg.lateScore,
                "score falls the longer a guest waits");

            // calm wave: one guest at a time, nobody waits
            var calm = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            var rush = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            var go = new GameObject("SelfTest_Satisfaction");
            try
            {
                calm.waves = new[] { new WaveConfig { guests = 3, spacing = 20f, weights = new float[] { 1, 0, 0, 0, 0 } } };
                var mc = new GameModel(calm);
                mc.StartWave();
                for (int i = 0; i < 900; i++) mc.Tick(0.1f);
                Check(!mc.IsBottleneck((int)NeedType.Rooms), "no bottleneck in a calm wave");
                Check(mc.S.satisfaction >= 99.9f && mc.WaveSatisfaction() >= 99.9f, "no waiting = 100 % satisfaction");

                // rush wave: 8 guests at the Hotel at once
                rush.waves = new[] { new WaveConfig { guests = 8, spacing = 0.1f, weights = new float[] { 1, 0, 0, 0, 0 } } };
                var mr = new GameModel(rush);
                var hud = go.AddComponent<HudView>();
                hud.Build(mr);
                mr.StartWave();
                Check(hud.SatisfactionBar.gameObject.activeSelf && hud.SatisfactionText.text.Contains("100%"), "bar shows 100 % at the start");
                for (int i = 0; i < 30; i++) mr.Tick(0.1f);
                Check(mr.IsBottleneck((int)NeedType.Rooms), "rush creates a bottleneck");
                float atBottleneck = mr.S.satisfaction;
                for (int i = 0; i < 60; i++) mr.Tick(0.1f);
                Check(mr.S.satisfaction < atBottleneck && mr.S.satisfaction < 100f, "satisfaction falls while the queue waits");
                for (int i = 0; i < 1200; i++) mr.Tick(0.1f);
                Check(mr.Stats((int)NeedType.Rooms).failed > 0, "guests that wait too long are lost");
                Check(mr.WaveSatisfaction() < 50f, "a long bottleneck ruins the satisfaction");
                Check(hud.SatisfactionText.text.Contains($"{Mathf.RoundToInt(mr.S.satisfaction)}%"), "bar follows the satisfaction");
                Check(hud.SatisfactionFace.sprite == Art.Get(Art.FaceIcon(mr.S.satisfaction)), "smiley follows the satisfaction");
            }
            finally { Dispose(go); Dispose(calm); Dispose(rush); }
        }
    

        // ------------------------------------------------------------------ SCRUM-59

        static void WaveEndBacklog()
        {
            var rush = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            var timed = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            var go = new GameObject("SelfTest_WaveEnd");
            try
            {
                // 8 guests reach the Hotel at once: a long backlog after the last arrival
                rush.waves = new[] { new WaveConfig { guests = 8, spacing = 0.1f, weights = new float[] { 1, 0, 0, 0, 0 } } };
                var m = new GameModel(rush);
                var hud = go.AddComponent<HudView>();
                hud.Build(m);
                int closed = 0, ended = 0;
                m.ArrivalsClosed += () => closed++;
                m.WaveEnded += () => ended++;
                m.StartWave();
                Check(m.ArrivalsOpen && m.Phase == GamePhase.WaveActive, "arrivals open at the start");
                for (int i = 0; i < 30; i++) m.Tick(0.1f);
                Check(!m.ArrivalsOpen && closed == 1 && m.GuestsToCome == 0, "arrivals close after the last guest");
                Check(m.Phase == GamePhase.WaveFinishing, "WAVE_ACTIVE -> WAVE_FINISHING");
                Check(m.QueuedRequests() > 0 && m.RemainingRequests() > 0, "requests are still waiting in the queue");
                Check(!m.StartWave(), "no new wave while the last guests are served");
                Check(hud.StatusText.text.Contains("finishing") && hud.StatusText.text.Contains($"{m.RemainingRequests()} requests left"),
                    "HUD shows the remaining requests");

                bool neverEarly = true;
                int guard = 0;
                while (!m.WaveComplete && guard++ < 5000)
                {
                    neverEarly &= m.RemainingRequests() > 0 || m.QueuedRequests() == 0;
                    m.Tick(0.1f);
                    if (m.WaveComplete) break;
                    neverEarly &= m.RemainingRequests() > 0;   // still running = something is left
                }
                Check(neverEarly, "the wave never ends while requests are left");
                Check(m.WaveComplete && ended == 1, "the wave ends once the queues are empty");
                var st = m.Stats((int)NeedType.Rooms);
                Check(st.Resolved == st.requested && st.requested == 8, "every request served or lost");
                Check(Mathf.Abs(m.S.satisfaction - m.WaveSatisfaction()) < 0.001f, "final satisfaction is fixed at the end");
                float t = m.S.waveTime;
                m.Tick(1f);
                Check(m.S.waveTime == t && ended == 1, "nothing runs after the wave ended");
                Check(hud.StatusText.text.Contains("complete"), "HUD shows the completed wave");

                // arrivals can also stop after a time limit: guests that did not come are dropped
                timed.waves = new[] { new WaveConfig { guests = 10, spacing = 1f, duration = 4.5f } };
                var mt = new GameModel(timed);
                mt.StartWave();
                for (int i = 0; i < 50; i++) mt.Tick(0.1f);
                int requested = 0;
                foreach (var s in mt.S.stats) requested += s.requested;
                Check(!mt.ArrivalsOpen && mt.S.plans.Count == 5, "arrivals stop after the wave duration (5 of 10 guests came)");
                Check(requested == 5, "requests of guests that did not come are dropped");
            }
            finally { Dispose(go); Dispose(rush); Dispose(timed); }
        }

        // ------------------------------------------------------------------ SCRUM-86

        /// <summary>Every kind of worker move (assign, take out, MOVE, + from another building) keeps coins and energy.</summary>
        static void FreeMoves(GameConfig cfg)
        {
            int rooms = (int)NeedType.Rooms, drinks = (int)NeedType.Drinks, food = (int)NeedType.Food;
            var m = new GameModel(cfg);
            Check(cfg.workerMoveCost == 0 && m.MoveCost == 0, "a move costs 0 coins");
            int coins = m.S.coins, energy = m.S.energy;

            Check(m.UnassignWorker(rooms) && m.AssignWorker(drinks), "take out + assign before the wave");
            Check(m.TransferWorker(drinks, rooms), "instant MOVE before the wave");
            Check(m.S.coins == coins && m.S.energy == energy, "no coins or energy spent before the wave");

            m.StartWave();
            for (int i = 0; i < 20; i++) m.Tick(0.1f);
            Check(m.TransferWorker(food, drinks) && m.PullWorker(food), "MOVE and + during the wave");
            Check(m.S.coins == coins && m.S.energy == energy, "no coins or energy spent during the wave");

            // the player can move workers even with an empty wallet
            m.S.coins = 0;
            m.S.energy = 0;
            Check(m.CanAffordMove && m.TransferWorker(rooms, food), "moves work with 0 coins");
            Check(m.S.coins == 0 && m.S.energy == 0, "the wallet stays at 0");
            Check(m.AssignedWorkers + m.FreeWorkers == m.S.totalWorkers, "no worker lost by the free moves");

            // the UI says so too
            var go = new GameObject("SelfTest_FreeMoves");
            try
            {
                var panel = go.AddComponent<WorkerPanel>();
                var hud = go.AddComponent<HudView>();
                hud.Build(m);
                panel.Build(m, hud.Root, () => drinks, d => { }, () => { }, () => { }, () => false);
                panel.Refresh();
                Check(panel.FreeText.text.Contains("moves are free"), "worker panel says moves are free");
                Check(panel.MoveTo.interactable, "MOVE is enabled with 0 coins");
            }
            finally { Dispose(go); }
        }

        // ------------------------------------------------------------------ SCRUM-92

        /// <summary>Seconds of wave time until the only Hotel guest of the wave is served.</summary>
        static float TimeToServe(GameModel m, int need, Action<GameModel> atHalf = null)
        {
            m.StartWave();
            bool half = false;
            for (int i = 0; i < 5000 && m.Stats(need).served == 0; i++)
            {
                m.Tick(0.05f);
                if (!half && atHalf != null)
                    foreach (var g in m.S.guests)
                        if (g.stage == GuestStage.InService && g.progress >= 0.5f) { half = true; atHalf(m); break; }
            }
            return m.Stats(need).served == 1 ? m.S.waveTime : float.PositiveInfinity;
        }

        static void WorkerPerformance()
        {
            int rooms = (int)NeedType.Rooms, drinks = (int)NeedType.Drinks;
            var cfg = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                cfg.patience = 1000f;   // nobody leaves: we only measure the speed
                cfg.waves = new[] { new WaveConfig { guests = 1, spacing = 1f, weights = new float[] { 1, 0, 0, 0, 0 } } };

                var m0 = new GameModel(cfg);
                Check(Mathf.Approximately(m0.WorkerSpeed(0), cfg.noWorkerFactor), "no worker = noWorkerFactor speed");
                Check(Mathf.Approximately(m0.WorkerSpeed(1), 1f), "1 worker = normal speed");
                Check(Mathf.Approximately(m0.WorkerSpeed(2), 1f + cfg.extraWorkerBonus), "2 workers = faster");
                Check(m0.WorkerSpeed(0) < m0.WorkerSpeed(1) && m0.WorkerSpeed(1) < m0.WorkerSpeed(3), "more workers, more speed");
                Check(Mathf.Approximately(m0.ServiceTime(rooms), cfg.Need(rooms).baseServiceTime), "1 worker serves in baseServiceTime");
                Check(m0.Speed((int)NeedType.Relax) == 0f, "a locked building does not serve");

                // the same guest, three staffing variants
                var one = new GameModel(cfg);
                float tOne = TimeToServe(one, rooms);

                var none = new GameModel(cfg);
                none.UnassignWorker(rooms);
                Check(none.IsUnstaffed(rooms) && none.ServiceTime(rooms) > one.ServiceTime(rooms), "Hotel without a worker is slower");
                float tNone = TimeToServe(none, rooms);

                var two = new GameModel(cfg);
                two.UnassignWorker(drinks);
                two.AssignWorker(rooms);
                float tTwo = TimeToServe(two, rooms);

                Check(!float.IsInfinity(tNone), "a building without a worker still serves (slowly)");
                Check(tNone > tOne && tOne > tTwo, $"service time: no worker {tNone:0.0} s > 1 worker {tOne:0.0} s > 2 workers {tTwo:0.0} s");
                float walk = cfg.walkTime;
                float ratio = (tNone - walk) / Mathf.Max(0.01f, tOne - walk);
                Check(Mathf.Abs(ratio - 1f / cfg.noWorkerFactor) < 0.3f, $"no worker is {ratio:0.0}x slower (expected {1f / cfg.noWorkerFactor:0.0}x)");

                // the worker leaves in the middle of the service: the guest keeps his progress, the rest is slow
                var moved = new GameModel(cfg);
                float progressAtMove = -1;
                float tMoved = TimeToServe(moved, rooms, mm =>
                {
                    foreach (var g in mm.S.guests) if (g.stage == GuestStage.InService) progressAtMove = g.progress;
                    mm.TransferWorker(rooms, drinks);
                    foreach (var g in mm.S.guests) if (g.stage == GuestStage.InService) Check(Mathf.Approximately(g.progress, progressAtMove), "guest keeps the service progress after the move");
                });
                Check(progressAtMove >= 0.5f && tMoved > tOne && tMoved < tNone, "worker moved away mid-service: slower than 1 worker, faster than none");
            }
            finally { Dispose(cfg); }
        }

        // ------------------------------------------------------------------ SCRUM-100

        static void WaveCompleteScreen()
        {
            var cfg = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            var go = new GameObject("SelfTest_WaveComplete");
            try
            {
                // a rush at the Hotel: some guests are lost, so the Hotel needs improvement
                cfg.waves = new[] { new WaveConfig { guests = 8, spacing = 0.1f, weights = new float[] { 1, 0, 0, 0, 0 } } };
                var m = new GameModel(cfg);
                var hud = go.AddComponent<HudView>();
                hud.Build(m);
                var screen = go.AddComponent<WaveCompleteView>();
                screen.Build(m, hud.Root);
                Check(!screen.IsOpen && m.LastResult == null, "screen is closed before any wave ended");

                m.StartWave();
                for (int i = 0; i < 50; i++) m.Tick(0.1f);
                Check(!screen.IsOpen, "screen stays closed during the wave");
                for (int i = 0; i < 5000 && !m.WaveComplete; i++) m.Tick(0.1f);

                var r = m.LastResult;
                Check(m.WaveComplete && r != null && r.wave == 1, "the finished wave leaves a result");
                Check(screen.IsOpen && screen.Result == r, "screen opens by itself with that result");
                Check(screen.TitleText.text == "WAVE 1 COMPLETE", "title names the wave");
                Check(r.stars == cfg.Stars(r.satisfaction) && screen.StarsShown == r.stars, $"{r.stars} star(s) for {r.satisfaction:0} % satisfaction");
                Check(screen.SatisfactionText.text.Contains($"{Mathf.RoundToInt(r.satisfaction)}%"), "screen shows the reached satisfaction");
                Check(screen.SatisfactionFace.sprite == Art.Get(Art.FaceIcon(r.satisfaction)), "smiley matches the satisfaction");

                var w = m.WaveRewards();
                Check(r.reward.coins == w.coins && r.reward.coins > 0, "coins reward is shown");
                Check(screen.CoinsReward.text == "+" + r.reward.coins && screen.EnergyReward.text == "+" + r.reward.energy
                      && screen.ValueReward.text == "+" + r.reward.resortValue, "rewards on the screen match the result");
                Check(m.S.coins == cfg.startCoins, "rewards are only shown, not added yet");

                int rooms = (int)NeedType.Rooms;
                Check(screen.RowVisible(rooms) && !screen.RowVisible((int)NeedType.Drinks), "only requested services have a row");
                Check(screen.RowCount(rooms) == $"{r.stats[rooms].served}/{r.stats[rooms].requested}", "row shows served / requested");
                Check(r.stats[rooms].failed > 0 && screen.RowState(rooms).StartsWith("needs improvement") && screen.RowState(rooms).Contains("lost"),
                    "Hotel with lost guests needs improvement");

                // the snapshot does not change any more
                m.S.stats[rooms].served += 100;
                m.Tick(0.1f);
                Check(r.stats[rooms].served != m.S.stats[rooms].served && screen.RowCount(rooms) == $"{r.stats[rooms].served}/{r.stats[rooms].requested}",
                    "screen keeps the finished wave's numbers");

                screen.ContinueButton.onClick.Invoke();
                Check(!screen.IsOpen, "CONTINUE closes the screen");
                m.NotifyChanged();
                Check(!screen.IsOpen, "the closed screen does not open again for the same wave");

                // a calm wave: every service worked well
                var calm = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
                try
                {
                    calm.waves = new[] { new WaveConfig { guests = 3, spacing = 20f, weights = new float[] { 1, 0, 0, 0, 0 } } };
                    var mc = new GameModel(calm);
                    mc.StartWave();
                    for (int i = 0; i < 5000 && !mc.WaveComplete; i++) mc.Tick(0.1f);
                    var screen2 = go.AddComponent<WaveCompleteView>();
                    screen2.Build(mc, hud.Root);
                    Check(screen2.IsOpen && screen2.StarsShown == 3, "calm wave: 3 stars (screen built after the wave also opens)");
                    Check(screen2.RowState(rooms) == "worked well", "calm wave: Hotel worked well");
                }
                finally { Dispose(calm); }
            }
            finally { Dispose(go); Dispose(cfg); }
        }

        // ------------------------------------------------------------------ SCRUM-108

        static void ResourceUpdate()
        {
            var cfg = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            var go = new GameObject("SelfTest_Resources");
            try
            {
                var m = new GameModel(cfg);
                int coinsEv = 0, energyEv = 0, valueEv = 0, claimEv = 0;
                m.CoinsAdded += a => coinsEv++;
                m.EnergyAdded += a => energyEv++;
                m.ResortValueChanged += d => valueEv++;
                m.RewardsClaimed += r => claimEv++;

                // single resources
                int c0 = m.S.coins;
                Check(m.AddCoins(15) == 15 && m.S.coins == c0 + 15 && coinsEv == 1, "AddCoins adds coins");
                Check(m.AddCoins(0) == 0 && m.AddCoins(-7) == 0 && m.S.coins == c0 + 15 && coinsEv == 1, "0 or negative coins change nothing");

                int e0 = m.S.energy;
                Check(m.AddEnergy(2) == 2 && m.S.energy == e0 + 2 && energyEv == 1, "AddEnergy adds energy");
                int added = m.AddEnergy(1000);
                Check(m.S.energy == m.MaxEnergy && added == m.MaxEnergy - (e0 + 2), "energy stops at MaxEnergy");
                Check(m.AddEnergy(5) == 0 && m.S.energy == m.MaxEnergy, "full energy gets nothing more");

                Check(m.AddResortValue(40) == 40 && m.S.resortValue == 40, "AddResortValue raises the value");
                Check(m.AddResortValue(-100) == -40 && m.S.resortValue == 0, "Resort Value never drops below 0");
                Check(valueEv == 2, "Resort Value changes are reported");

                // claim the rewards of a real wave
                cfg.waves = new[] { new WaveConfig { guests = 3, spacing = 20f, weights = new float[] { 1, 0, 0, 0, 0 } } };
                var w = new GameModel(cfg);
                var hud = go.AddComponent<HudView>();
                hud.Build(w);
                var screen = go.AddComponent<WaveCompleteView>();
                screen.Build(w, hud.Root);
                int claims = 0;
                w.RewardsClaimed += r => claims++;
                Check(!w.CanClaimRewards && !w.ClaimRewards(), "nothing to claim before a wave");

                w.StartWave();
                for (int i = 0; i < 50; i++) w.Tick(0.1f);
                Check(!w.ClaimRewards(), "nothing to claim during the wave");
                for (int i = 0; i < 5000 && !w.WaveComplete; i++) w.Tick(0.1f);

                var res = w.LastResult;
                var calc = RewardCalculator.Calculate(cfg, res.stats, res.satisfaction, 0);
                Check(calc.coins == res.reward.coins && calc.energy == res.reward.energy && calc.resortValue == res.reward.resortValue,
                    "RewardCalculator gives the same reward from the wave's numbers");
                Check(res.reward.coins > 0 && res.reward.energy > 0 && res.reward.resortValue > 0, "a good wave earns coins, energy and resort value");

                int coins = w.S.coins, energy = w.S.energy, value = w.S.resortValue;
                Check(w.CanClaimRewards && screen.ClaimButton.interactable, "rewards can be claimed on the Wave Complete screen");
                screen.ClaimButton.onClick.Invoke();
                Check(w.S.coins == coins + res.reward.coins, "Claim adds the Coins");
                Check(w.S.energy == Mathf.Min(w.MaxEnergy, energy + res.reward.energy), "Claim adds the Energy");
                Check(w.S.resortValue == value + res.reward.resortValue, "Claim updates the Resort Value");
                Check(hud.CoinsText.text == w.S.coins.ToString() && hud.EnergyText.text == w.S.energy.ToString()
                      && hud.ValueText.text == w.S.resortValue.ToString(), "HUD shows the new resources");
                Check(!screen.IsOpen && claims == 1, "screen closes after the claim");

                // the same reward only once
                int c1 = w.S.coins, e1 = w.S.energy, v1 = w.S.resortValue;
                Check(!w.ClaimRewards() && w.RewardsAlreadyClaimed && !w.CanClaimRewards, "second claim is refused");
                screen.Claim();
                Check(w.S.coins == c1 && w.S.energy == e1 && w.S.resortValue == v1 && claims == 1, "a second press adds nothing");
                Check(!screen.ClaimButton.interactable && screen.ClaimButton.GetComponentInChildren<UnityEngine.UI.Text>().text == "CLAIMED",
                    "button shows CLAIMED and is disabled");
            }
            finally { Dispose(go); Dispose(cfg); }
        }
    }
}
