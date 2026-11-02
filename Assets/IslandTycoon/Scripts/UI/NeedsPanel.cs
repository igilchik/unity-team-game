using UnityEngine;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>
    /// SCRUM-42: Wave Needs Panel on the left edge, so the island stays visible.
    /// One row per need guests can ask for (icon + name).
    /// </summary>
    public class NeedsPanel : MonoBehaviour
    {
        static readonly Vector2 Top = new Vector2(0.5f, 1);
        static readonly Color RowColor = new Color(0.55f, 0.33f, 0.14f, 0.13f);
        const float RowStep = 92;

        GameModel _m;
        RectTransform _panel;
        Text _header;
        readonly Image[] _rowBg = new Image[Needs.Count];
        readonly Text[] _rowName = new Text[Needs.Count];

        public RectTransform Panel => _panel;
        public Text Header => _header;
        public bool RowVisible(int need) => _rowBg[need] != null && _rowBg[need].gameObject.activeSelf;
        public string RowName(int need) => _rowName[need].text;

        /// <summary>Builds the panel inside the given UI root (the HUD safe area).</summary>
        public void Build(GameModel model, Transform root)
        {
            _m = model;
            var content = UIFactory.Frame("NeedsPanel", root, new Vector2(0, 0.5f), new Vector2(16, 10), new Vector2(360, 600), out _panel, 70f);
            _panel.pivot = new Vector2(0, 0.5f);
            _panel.GetComponent<Image>().raycastTarget = false;
            _header = UIFactory.Label("Header", content, "GUEST NEEDS", 30, UIFactory.Brown, Top, new Vector2(0, -20), new Vector2(290, 44));

            for (int n = 0; n < Needs.Count; n++)
            {
                var row = UIFactory.Panel("Row_" + (NeedType)n, content, Top, new Vector2(0, -70), new Vector2(286, 84), RowColor);
                row.raycastTarget = false;
                _rowBg[n] = row;
                UIFactory.Icon("Icon", row.transform, Art.Get(Art.NeedIcon(_m.Cfg, n)), new Vector2(0, 0.5f), new Vector2(42, 0), 68);
                _rowName[n] = UIFactory.Text("Name", row.transform, _m.Cfg.Need(n).displayName, 26, UIFactory.Brown, TextAnchor.MiddleLeft,
                    new Vector2(0, 0.5f), new Vector2(84, 15), new Vector2(160, 34));
                _rowName[n].rectTransform.pivot = new Vector2(0, 0.5f);
            }
            Layout();
        }

        /// <summary>Shows only the needs guests can ask for now and stacks them from the top.</summary>
        void Layout()
        {
            int visible = 0;
            for (int n = 0; n < Needs.Count; n++)
            {
                bool show = _m.IsNeedActive(n);
                UIFactory.SetActive(_rowBg[n], show);
                if (!show) continue;
                _rowBg[n].rectTransform.anchoredPosition = new Vector2(0, -50 - visible * RowStep);
                visible++;
            }
            _panel.sizeDelta = new Vector2(360, 128 + visible * RowStep);
        }
    }
}
