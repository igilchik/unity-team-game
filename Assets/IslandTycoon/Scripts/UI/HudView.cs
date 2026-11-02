using UnityEngine;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>
    /// Screen-space HUD over the island.
    /// SCRUM-17: current state of the resort in the title plaque.
    /// SCRUM-18: Coins. SCRUM-19: Energy. SCRUM-20: Resort Value.
    /// SCRUM-21: every value is read from the current game data and refreshed on GameModel.Changed.
    /// SCRUM-23: the plaque shows the wave number and the AWAITING_WAVE state.
    /// SCRUM-26: ... and the running wave.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        static readonly Vector2 Top = new Vector2(0.5f, 1);

        GameModel _m;
        RectTransform _root;
        Text _title, _status, _coins, _energy, _value;

        public Text TitleText => _title;
        public Text StatusText => _status;
        public Text CoinsText => _coins;
        public Text EnergyText => _energy;
        public Text ValueText => _value;

        public void Build(GameModel model)
        {
            _m = model;
            var canvas = UIFactory.Canvas("HudCanvas", 10);
            canvas.transform.SetParent(transform, false);
            _root = UIFactory.Rect("SafeArea", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            UIFactory.Stretch(_root);
            _root.gameObject.AddComponent<SafeArea>();

            // resort state plaque (top centre)
            UIFactory.WoodPill("StatePlaque", _root, Top, new Vector2(170, -10), new Vector2(620, 118), 80f, out var pin);
            _title = UIFactory.FitText("Title", pin, "", 40, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.42f), new Vector2(1, 1), true);
            _status = UIFactory.FitText("Status", pin, "", 22, UIFactory.Cream, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0.46f));

            // currencies (top left)
            var tl = new Vector2(0, 1);
            _coins = Pill("Coins", "UI/Icons/icon_coin", tl, new Vector2(20, -16));
            _energy = Pill("Energy", "UI/Icons/icon_energy", tl, new Vector2(270, -16));
            _value = Pill("ResortValue", "UI/Icons/icon_resort_value", tl, new Vector2(520, -16));

            _m.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_m != null) _m.Changed -= Refresh;
        }

        /// <summary>Copies the current game data into the HUD (only texts that really changed are touched).</summary>
        public void Refresh()
        {
            var s = _m.S;
            UIFactory.SetText(_title, "WAVE " + _m.WaveNumber);
            UIFactory.SetText(_status, ResortStatus());
            UIFactory.SetText(_coins, s.coins.ToString());
            UIFactory.SetText(_energy, s.energy.ToString());
            UIFactory.SetText(_value, s.resortValue.ToString());
        }

        /// <summary>Wooden pill with an icon on the left and a number inside.</summary>
        Text Pill(string name, string icon, Vector2 anchor, Vector2 pos)
        {
            var p = UIFactory.WoodPill(name, _root, anchor, pos, new Vector2(234, 80), 70f, out var inner);
            UIFactory.Icon("Icon", p.transform, Art.Get(icon), new Vector2(0, 0.5f), new Vector2(32, 2), 76);
            return UIFactory.FitText("Value", inner, "0", 34, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, true);
        }

        /// <summary>One line describing the resort right now.</summary>
        string ResortStatus()
        {
            string buildings = $"{_m.OpenBuildings} of {CountPlots()} buildings open";
            switch (_m.Phase)
            {
                case GamePhase.AwaitingWave: return $"{_m.IC.name} · {buildings} · get ready!";
                case GamePhase.WaveActive:
                    int t = (int)_m.S.waveTime;
                    return $"{_m.IC.name} · wave in progress {t / 60}:{t % 60:00}";
                default: return $"{_m.IC.name} · {buildings}";
            }
        }

        int CountPlots()
        {
            int n = 0;
            foreach (var b in _m.S.buildings) if (b.exists) n++;
            return n;
        }
    }
}
