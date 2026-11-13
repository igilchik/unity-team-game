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
    }
}
