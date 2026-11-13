using UnityEngine;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>
    /// SCRUM-42: Wave Needs Panel on the left edge, so the island stays visible.
    /// One row per need guests can ask for (icon + name).
    /// SCRUM-43: aggregated state of every need (served / requested in the wave).
    /// SCRUM-44: the values follow the active wave live (served, guests waiting).
    /// SCRUM-46: a bottleneck turns its row red with a pulsing warning sign.
    /// </summary>
    public class NeedsPanel : MonoBehaviour
    {
        static readonly Vector2 Top = new Vector2(0.5f, 1);
        static readonly Color RowColor = new Color(0.55f, 0.33f, 0.14f, 0.13f);
        static readonly Color BottleneckColor = new Color(0.9f, 0.3f, 0.25f, 0.35f);
        const float RowStep = 92;

        GameModel _m;
        RectTransform _panel;
        Text _header;
        readonly Image[] _rowBg = new Image[Needs.Count];
        readonly Text[] _rowName = new Text[Needs.Count];
        readonly Text[] _rowValue = new Text[Needs.Count];
        readonly Image[] _rowWarn = new Image[Needs.Count];

        public RectTransform Panel => _panel;
        public Text Header => _header;
        public bool RowVisible(int need) => _rowBg[need] != null && _rowBg[need].gameObject.activeSelf;
        public string RowName(int need) => _rowName[need].text;
        public string RowValue(int need) => _rowValue[need].text;
        public bool RowWarning(int need) => _rowWarn[need] != null && _rowWarn[need].gameObject.activeSelf;

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
                _rowValue[n] = UIFactory.Text("Value", row.transform, "", 22, UIFactory.Brown, TextAnchor.MiddleLeft,
                    new Vector2(0, 0.5f), new Vector2(84, -17), new Vector2(170, 30));
                _rowValue[n].rectTransform.pivot = new Vector2(0, 0.5f);
                _rowWarn[n] = UIFactory.Icon("Warn", row.transform, Art.Get("UI/Icons/icon_warning"), new Vector2(1, 0.5f), new Vector2(-30, 0), 48);
                _rowWarn[n].gameObject.SetActive(false);
            }
            _m.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_m != null) _m.Changed -= Refresh;
        }

        /// <summary>Rows, header and the aggregated value of every need.</summary>
        public void Refresh()
        {
            Arrange();
            bool active = _m.InWave;
            UIFactory.SetText(_header, active ? $"WAVE {_m.WaveNumber} NEEDS" : "GUEST NEEDS");
            for (int n = 0; n < Needs.Count; n++)
            {
                if (!RowVisible(n)) continue;
                UIFactory.SetText(_rowValue[n], active ? Aggregate(n) : "<color=#8A7F70>no guests yet</color>");
                bool bottleneck = _m.IsBottleneck(n);
                UIFactory.SetActive(_rowWarn[n], bottleneck);
                _rowBg[n].color = bottleneck ? BottleneckColor : RowColor;
            }
        }

        void Update()
        {
            // pulse the warning signs so a bottleneck catches the eye
            float k = 1f + Mathf.Sin(Time.time * 8f) * 0.1f;
            foreach (var w in _rowWarn)
                if (w != null && w.gameObject.activeSelf) w.transform.localScale = new Vector3(k, k, 1);
        }

        /// <summary>Aggregated state of one need, e.g. "2/5 · 1 wait".</summary>
        string Aggregate(int need)
        {
            var st = _m.Stats(need);
            string color = _m.IsBottleneck(need) ? "#C0392B" : "#2E7D32";
            string lost = st.failed > 0 ? $" · <color=#C0392B>{st.failed} lost</color>" : "";
            return $"<color={color}>{st.served}/{st.requested} · {_m.QueueCount(need)} wait</color>{lost}";
        }

        /// <summary>Shows only the needs guests can ask for now and stacks them from the top.</summary>
        void Arrange()
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
