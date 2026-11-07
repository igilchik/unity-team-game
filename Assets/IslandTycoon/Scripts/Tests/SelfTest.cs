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
    }
}
