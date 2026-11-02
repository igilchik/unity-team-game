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

        static void Dispose(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
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
        }
    }
}
