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
        static readonly Color StarOff = new Color(0.25f, 0.25f, 0.25f, 0.45f);
        Button _continue;
        int _shownFor = -1;   // wave number the screen was already opened for

        public RectTransform Modal => _modal;
        public Text TitleText => _title;
        public Text SummaryText => _summary;
        public Text SatisfactionText => _satisfaction;
        public Image SatisfactionFace => _face;
        public Text CoinsReward => _reward[0];
        public Text EnergyReward => _reward[1];
        public Text ValueReward => _reward[2];
        /// <summary>Stars lit on the screen.</summary>
        public int StarsShown { get { int n = 0; foreach (var s in _stars) if (s.color == Color.white) n++; return n; } }
        public Button ContinueButton => _continue;
        public bool IsOpen => UIFactory.IsOpen(_modal);

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
            _continue = UIFactory.Button("Continue", _content, "UI/Kit/btn_primary_orange", "CONTINUE", 44, Bottom, new Vector2(0, 72),
                new Vector2(310, 0), Close, true);
            _m.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_m != null) _m.Changed -= Refresh;
        }

        /// <summary>Opens the screen once when the wave is complete.</summary>
        public void Refresh()
        {
            if (_m.WaveComplete && _shownFor != _m.WaveNumber)
            {
                _shownFor = _m.WaveNumber;
                Fill();
                UIFactory.Open(_modal);
            }
            else if (!_m.WaveComplete && IsOpen) UIFactory.Close(_modal);
        }

        /// <summary>Writes the finished wave into the screen.</summary>
        void Fill()
        {
            UIFactory.SetText(_title, $"WAVE {_m.WaveNumber} COMPLETE");
            int stars = _m.WaveStars;
            for (int i = 0; i < 3; i++) _stars[i].color = i < stars ? Color.white : StarOff;
            UIFactory.SetText(_summary, GameModel.RatingWord(stars));
            _summary.color = stars >= 2 ? UIFactory.Green : stars == 1 ? UIFactory.Brown : UIFactory.Red;

            float sat = _m.S.satisfaction;
            UIFactory.SetSprite(_face, Art.Get(Art.FaceIcon(sat)));
            UIFactory.SetText(_satisfaction, $"Satisfaction {Mathf.RoundToInt(sat)}%");

            var r = _m.WaveRewards();
            UIFactory.SetText(_reward[0], "+" + r.coins);
            UIFactory.SetText(_reward[1], "+" + r.energy);
            UIFactory.SetText(_reward[2], "+" + r.resortValue);
        }

        public void Close() => UIFactory.Close(_modal);
    }
}
