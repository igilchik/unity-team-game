using System;
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
    /// SCRUM-24: START WAVE button, the primary action in the bottom-right corner (thumb reach).
    /// SCRUM-25: the button is shown and active only in AWAITING_WAVE.
    /// SCRUM-78: free workers counter (bottom left) follows every worker move.
    /// SCRUM-52: Wave Satisfaction bar with a smiley under the plaque during the wave.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        static readonly Vector2 Top = new Vector2(0.5f, 1);

        GameModel _m;
        RectTransform _root;
        Text _title, _status, _coins, _energy, _value, _freeWorkers;
        Button _startWave;
        RectTransform _satBar, _satFill;
        Image _satFace;
        Text _satText;
        const float SatWidth = 430, SatHeight = 72;

        public Text TitleText => _title;
        public Text StatusText => _status;
        public Text CoinsText => _coins;
        public Text EnergyText => _energy;
        public Text ValueText => _value;
        public Button StartWaveButton => _startWave;
        public Text FreeWorkersText => _freeWorkers;
        public RectTransform SatisfactionBar => _satBar;
        public Text SatisfactionText => _satText;
        public Image SatisfactionFace => _satFace;
        /// <summary>Safe-area root for other HUD panels.</summary>
        public RectTransform Root => _root;

        public void Build(GameModel model, Action onStartWave = null)
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

            // Wave Satisfaction bar (under the plaque, only during the wave)
            _satBar = UIFactory.Centered("Satisfaction", _root, Top, new Vector2(150, -166), new Vector2(SatWidth, SatHeight));
            var empty = UIFactory.Image("Empty", _satBar, Art.Get("UI/Kit/bar_empty"), new Vector2(0, 0.5f), Vector2.zero, new Vector2(SatWidth, SatHeight));
            empty.preserveAspect = false;
            _satFill = UIFactory.Rect("FillMask", _satBar, new Vector2(0, 0.5f), Vector2.zero, new Vector2(SatWidth, SatHeight));
            _satFill.gameObject.AddComponent<RectMask2D>();
            var full = UIFactory.Image("Full", _satFill, Art.Get("UI/Kit/bar_full"), new Vector2(0, 0.5f), Vector2.zero, new Vector2(SatWidth, SatHeight));
            full.preserveAspect = false;
            _satFace = UIFactory.Icon("Face", _satBar, Art.Get("UI/Icons/icon_face_happy"), new Vector2(1, 0.5f), new Vector2(34, 0), 76);
            _satText = UIFactory.FitText("Pct", _satBar, "100%", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0.22f, 0.2f), new Vector2(0.95f, 0.85f), true);

            // currencies (top left)
            var tl = new Vector2(0, 1);
            _coins = Pill("Coins", "UI/Icons/icon_coin", tl, new Vector2(20, -16));
            _energy = Pill("Energy", "UI/Icons/icon_energy", tl, new Vector2(270, -16));
            _value = Pill("ResortValue", "UI/Icons/icon_resort_value", tl, new Vector2(520, -16));

            // free workers (bottom left)
            var wp = UIFactory.WoodPill("FreeWorkers", _root, Vector2.zero, new Vector2(20, 20), new Vector2(330, 88), 80f, out var win);
            UIFactory.Icon("Icon", wp.transform, Art.Get("Characters/Worker/worker_wave"), new Vector2(0, 0.5f), new Vector2(42, 6), 92);
            _freeWorkers = UIFactory.FitText("Text", win, "", 28, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, true);

            // primary action (bottom right)
            _startWave = UIFactory.Button("StartWave", _root, "UI/Kit/btn_primary_orange", "START WAVE", 46,
                new Vector2(1, 0), new Vector2(-24, 20), new Vector2(370, 0), () => { if (_m.CanStartWave) onStartWave?.Invoke(); });

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
            UIFactory.SetText(_freeWorkers, $"Free workers: {_m.FreeWorkers}");

            UIFactory.SetActive(_startWave, _m.CanStartWave);
            _startWave.interactable = _m.CanStartWave;

            bool wave = _m.InWave;
            UIFactory.SetActive(_satBar, wave);
            if (wave)
            {
                float sat = s.satisfaction;
                _satFill.sizeDelta = new Vector2(SatWidth * Mathf.Clamp01(sat / 100f), SatHeight);
                UIFactory.SetText(_satText, $"Satisfaction {Mathf.RoundToInt(sat)}%");
                UIFactory.SetSprite(_satFace, Art.Get(Art.FaceIcon(sat)));
            }
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
                    if (_m.AllGuestsDone) return $"{_m.IC.name} · all guests served!";
                    return $"{_m.IC.name} · wave in progress {t / 60}:{t % 60:00} · {_m.GuestsToCome} guests arriving";
                case GamePhase.WaveFinishing:
                    if (_m.AllGuestsDone) return $"{_m.IC.name} · all guests served!";
                    return $"{_m.IC.name} · finishing the wave · no more guests · {_m.RemainingRequests()} requests left";
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
