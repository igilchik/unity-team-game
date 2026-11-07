using UnityEngine;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>
    /// Screen-space HUD over the island.
    /// SCRUM-17: current state of the resort in the title plaque.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        static readonly Vector2 Top = new Vector2(0.5f, 1);

        GameModel _m;
        RectTransform _root;
        Text _title, _status;

        public Text TitleText => _title;
        public Text StatusText => _status;

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

            _title.text = _m.IC.name.ToUpperInvariant();
            _status.text = ResortStatus();
        }

        /// <summary>One line describing the resort right now.</summary>
        string ResortStatus() => $"{_m.OpenBuildings} of {CountPlots()} buildings open";

        int CountPlots()
        {
            int n = 0;
            foreach (var b in _m.S.buildings) if (b.exists) n++;
            return n;
        }
    }
}
