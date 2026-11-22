using UnityEngine;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>
    /// SCRUM-94: Wave Complete screen. A modal over the island that opens by itself when the
    /// wave reaches WAVE_COMPLETE (every guest served or gone). CONTINUE closes it.
    /// </summary>
    public class WaveCompleteView : MonoBehaviour
    {
        protected static readonly Vector2 Top = new Vector2(0.5f, 1);
        protected static readonly Vector2 Bottom = new Vector2(0.5f, 0);

        GameModel _m;
        RectTransform _modal, _content;
        Text _title, _summary;
        Button _continue;
        int _shownFor = -1;   // wave number the screen was already opened for

        public RectTransform Modal => _modal;
        public Text TitleText => _title;
        public Text SummaryText => _summary;
        public Button ContinueButton => _continue;
        public bool IsOpen => UIFactory.IsOpen(_modal);

        public void Build(GameModel model, Transform root)
        {
            _m = model;
            _modal = UIFactory.Modal("WaveComplete", root, new Vector2(1040, 820), null, out _content);
            _title = UIFactory.Ribbon(_modal.Find("Panel"), "WAVE COMPLETE", 640);
            _summary = UIFactory.Label("Summary", _content, "", 30, UIFactory.Brown, Top, new Vector2(0, -60), new Vector2(820, 60));
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
            UIFactory.SetText(_summary, "All guests have left the island.");
        }

        public void Close() => UIFactory.Close(_modal);
    }
}
