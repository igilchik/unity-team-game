using UnityEngine;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>
    /// SCRUM-94: Wave Complete screen. A modal over the island that opens by itself when the
    /// wave reaches WAVE_COMPLETE (every guest served or gone). CONTINUE closes it.
    /// SCRUM-95: final rating, 0..3 stars and a word for it.
    /// SCRUM-96: Wave Satisfaction the wave reached, with a smiley.
    /// SCRUM-97: rewards earned in the wave: Coins, Energy and Resort Value.
    /// SCRUM-98: one row per service of the wave (served / requested); services that worked well in green.
    /// SCRUM-99: services that need improvement in red, with the reason (lost guests / slow service).
    /// SCRUM-100: connected to the finished wave: opens on GameModel.WaveEnded and shows only the
    /// snapshot of that wave (GameModel.LastResult), so nothing on it changes afterwards.
    /// SCRUM-106: CLAIM REWARDS adds the shown rewards to the player and closes the screen.
    /// SCRUM-107: the button works only once per wave; claimed rewards show CLAIMED and a disabled button.
    /// </summary>
    public class WaveCompleteView : MonoBehaviour
    {
        protected static readonly Vector2 Top = new Vector2(0.5f, 1);
        protected static readonly Vector2 Bottom = new Vector2(0.5f, 0);

        GameModel _m;
        RectTransform _modal, _content;
        Text _title, _summary;
        readonly Image[] _stars = new Image[3];
        Image _face;
        Text _satisfaction;
        readonly Text[] _reward = new Text[3];   // coins, energy, resort value
        readonly Image[] _rowIcon = new Image[Needs.Count];
        readonly Text[] _rowName = new Text[Needs.Count];
        readonly Text[] _rowCount = new Text[Needs.Count];
        readonly Text[] _rowState = new Text[Needs.Count];
        const float RowTop = -390, RowStep = 44;
        static readonly Color StarOff = new Color(0.25f, 0.25f, 0.25f, 0.45f);
        Button _continue;
        int _shownFor = -1;   // wave number the screen was already opened for
        WaveResult _result;

        public RectTransform Modal => _modal;
        public Text TitleText => _title;
        public Text SummaryText => _summary;
        public Text SatisfactionText => _satisfaction;
        public Image SatisfactionFace => _face;
        public Text CoinsReward => _reward[0];
        public Text EnergyReward => _reward[1];
        public Text ValueReward => _reward[2];
        public bool RowVisible(int need) => _rowName[need].gameObject.activeSelf;
        public string RowCount(int need) => _rowCount[need].text;
        public string RowState(int need) => _rowState[need].text;
        /// <summary>Stars lit on the screen.</summary>
        public int StarsShown { get { int n = 0; foreach (var s in _stars) if (s.color == Color.white) n++; return n; } }
        public Button ContinueButton => _continue;
        /// <summary>The CLAIM REWARDS button (same button as ContinueButton).</summary>
        public Button ClaimButton => _continue;
        public bool IsOpen => UIFactory.IsOpen(_modal);
        /// <summary>The wave results the screen shows.</summary>
        public WaveResult Result => _result;

        public void Build(GameModel model, Transform root)
        {
            _m = model;
            _modal = UIFactory.Modal("WaveComplete", root, new Vector2(1040, 900), null, out _content);
            _title = UIFactory.Ribbon(_modal.Find("Panel"), "WAVE COMPLETE", 640);

            // rating: three stars, the middle one bigger
            for (int i = 0; i < 3; i++)
                _stars[i] = UIFactory.Icon("Star" + i, _content, Art.Get("UI/Icons/icon_resort_value"), Top,
                    new Vector2((i - 1) * 140, i == 1 ? -78 : -90), i == 1 ? 132 : 112);
            _summary = UIFactory.Label("Rating", _content, "", 36, UIFactory.Brown, Top, new Vector2(0, -168), new Vector2(820, 52));

            // reached satisfaction: smiley + percent
            _face = UIFactory.Icon("Face", _content, null, Top, new Vector2(-170, -232), 64);
            _satisfaction = UIFactory.Text("Satisfaction", _content, "", 34, UIFactory.Brown, TextAnchor.MiddleLeft, Top,
                new Vector2(-125, -232), new Vector2(380, 56));
            _satisfaction.rectTransform.pivot = new Vector2(0, 0.5f);

            // rewards: same icons as the HUD pills
            string[] icons = { "UI/Icons/icon_coin", "UI/Icons/icon_energy", "UI/Icons/icon_resort_value" };
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 290;
                UIFactory.Icon("RewardIcon" + i, _content, Art.Get(icons[i]), Top, new Vector2(x - 70, -308), 58);
                _reward[i] = UIFactory.Text("Reward" + i, _content, "", 34, UIFactory.Brown, TextAnchor.MiddleLeft, Top,
                    new Vector2(x - 32, -308), new Vector2(180, 52));
                _reward[i].rectTransform.pivot = new Vector2(0, 0.5f);
            }

            var line = UIFactory.Panel("Divider", _content, Top, new Vector2(0, -352), new Vector2(820, 4), new Color(0.45f, 0.28f, 0.1f, 0.35f));
            line.raycastTarget = false;
            line.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // services of the wave
            for (int n = 0; n < Needs.Count; n++)
            {
                _rowIcon[n] = UIFactory.Icon("RowIcon" + n, _content, Art.Get(Art.NeedIcon(_m.Cfg, n)), Top, new Vector2(-330, 0), 38);
                _rowName[n] = UIFactory.Text("RowName" + n, _content, _m.Cfg.Need(n).displayName, 26, UIFactory.Brown, TextAnchor.MiddleLeft, Top,
                    new Vector2(-298, 0), new Vector2(230, 38));
                _rowName[n].rectTransform.pivot = new Vector2(0, 0.5f);
                _rowCount[n] = UIFactory.Label("RowCount" + n, _content, "", 26, UIFactory.Brown, Top, new Vector2(-10, 0), new Vector2(150, 38));
                _rowState[n] = UIFactory.Text("RowState" + n, _content, "", 24, UIFactory.Green, TextAnchor.MiddleLeft, Top,
                    new Vector2(90, 0), new Vector2(370, 38));
                _rowState[n].rectTransform.pivot = new Vector2(0, 0.5f);
            }
            _continue = UIFactory.Button("ClaimRewards", _content, "UI/Kit/btn_primary_orange", "CLAIM REWARDS", 40, Bottom, new Vector2(0, 72),
                new Vector2(400, 0), Claim, true);
            _m.WaveEnded += OnWaveEnded;
            _m.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_m == null) return;
            _m.WaveEnded -= OnWaveEnded;
            _m.Changed -= Refresh;
        }

        /// <summary>The wave just ended: show its results.</summary>
        void OnWaveEnded()
        {
            if (_m.LastResult != null) Show(_m.LastResult);
        }

        /// <summary>Opens the screen once per finished wave (also if it was built after the wave ended); closes it when a new wave starts.</summary>
        public void Refresh()
        {
            var r = _m.LastResult;
            if (_m.WaveComplete && r != null && _shownFor != r.wave) Show(r);
            else if (!_m.WaveComplete && IsOpen) UIFactory.Close(_modal);
        }

        /// <summary>Fills the screen with the results of a finished wave and opens it.</summary>
        public void Show(WaveResult r)
        {
            if (r == null) return;
            _result = r;
            _shownFor = r.wave;
            Fill(r);
            UIFactory.Open(_modal);
        }

        /// <summary>Writes the finished wave into the screen.</summary>
        void Fill(WaveResult r)
        {
            UIFactory.SetText(_title, $"WAVE {r.wave} COMPLETE");
            int stars = r.stars;
            for (int i = 0; i < 3; i++) _stars[i].color = i < stars ? Color.white : StarOff;
            UIFactory.SetText(_summary, GameModel.RatingWord(stars));
            _summary.color = stars >= 2 ? UIFactory.Green : stars == 1 ? UIFactory.Brown : UIFactory.Red;

            float sat = r.satisfaction;
            UIFactory.SetSprite(_face, Art.Get(Art.FaceIcon(sat)));
            UIFactory.SetText(_satisfaction, $"Satisfaction {Mathf.RoundToInt(sat)}%");

            UIFactory.SetText(_reward[0], "+" + r.reward.coins);
            UIFactory.SetText(_reward[1], "+" + r.reward.energy);
            UIFactory.SetText(_reward[2], "+" + r.reward.resortValue);
            UpdateClaimButton();

            // one row per need guests asked for, stacked from the top
            int row = 0;
            for (int n = 0; n < Needs.Count; n++)
            {
                var st = n < r.stats.Count ? r.stats[n] : null;
                bool show = st != null && st.requested > 0;
                UIFactory.SetActive(_rowIcon[n], show); UIFactory.SetActive(_rowName[n], show);
                UIFactory.SetActive(_rowCount[n], show); UIFactory.SetActive(_rowState[n], show);
                if (!show) continue;
                float y = RowTop - row * RowStep;
                foreach (Graphic g in new Graphic[] { _rowIcon[n], _rowName[n], _rowCount[n], _rowState[n] })
                    g.rectTransform.anchoredPosition = new Vector2(g.rectTransform.anchoredPosition.x, y);
                UIFactory.SetText(_rowCount[n], $"{st.served}/{st.requested}");
                bool good = _m.WorkedWell(st);
                UIFactory.SetText(_rowState[n], good ? "worked well" : $"needs improvement ({GameModel.ImprovementReason(st)})");
                _rowState[n].color = good ? UIFactory.Green : UIFactory.Red;
                row++;
            }
        }

        public void Close() => UIFactory.Close(_modal);

        /// <summary>Takes the rewards of the shown wave and closes the screen (only the first press counts).</summary>
        public void Claim()
        {
            if (_result == null || _result.rewardsClaimed || !_m.ClaimRewards()) { UpdateClaimButton(); return; }
            UpdateClaimButton();
            Close();
        }

        /// <summary>CLAIM REWARDS while the reward is free, CLAIMED (disabled) after it was taken.</summary>
        void UpdateClaimButton()
        {
            bool claimed = _result != null && _result.rewardsClaimed;
            _continue.interactable = !claimed;
            var label = _continue.GetComponentInChildren<Text>();
            if (label != null) UIFactory.SetText(label, claimed ? "CLAIMED" : "CLAIM REWARDS");
        }
    }
}
