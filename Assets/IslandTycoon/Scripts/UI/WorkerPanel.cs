using System;
using UnityEngine;
using UnityEngine.UI;

namespace IslandTycoon
{
    /// <summary>
    /// SCRUM-75: Worker Allocation UI. Tap a building: a panel at the bottom shows its
    /// workers with - / + buttons to take a worker out or send a free one in.
    /// SCRUM-76: moving is allowed before the wave starts.
    /// SCRUM-77: ... and during the wave; + takes a worker from the busiest building if none is free.
    /// SCRUM-78: the panel refreshes after every change of the game (moves, queue during the wave).
    /// </summary>
    public class WorkerPanel : MonoBehaviour
    {
        GameModel _m;
        Func<int> _selected;
        Action<int> _move;
        Action _close;
        RectTransform _panel;
        Image _icon;
        Text _title, _info, _workers, _free;
        Button _minus, _plus;
        int _shown = -2;

        public RectTransform Panel => _panel;
        public Text WorkersText => _workers;
        public Text FreeText => _free;
        public Text InfoText => _info;
        public Button Minus => _minus;
        public Button Plus => _plus;

        /// <summary>
        /// selected: building shown (-1 = none); move(delta): -1 / +1 worker for it; close: deselect.
        /// </summary>
        public void Build(GameModel model, Transform root, Func<int> selected, Action<int> move, Action close)
        {
            _m = model;
            _selected = selected;
            _move = move;
            _close = close;
            var bottom = new Vector2(0.5f, 0); var left = new Vector2(0, 0.5f); var tl = new Vector2(0, 1); var bl = new Vector2(0, 0);

            var content = UIFactory.Frame("WorkerPanel", root, bottom, new Vector2(0, 10), new Vector2(900, 300), out _panel, 78f);
            _icon = UIFactory.Icon("Icon", content, null, left, new Vector2(72, 10), 130);
            _title = UIFactory.Text("Title", content, "", 38, UIFactory.Brown, TextAnchor.MiddleLeft, tl, new Vector2(160, -26), new Vector2(560, 56));
            _title.rectTransform.pivot = new Vector2(0, 0.5f);
            _info = UIFactory.Text("Info", content, "", 25, UIFactory.Brown, TextAnchor.MiddleLeft, tl, new Vector2(160, -74), new Vector2(560, 50));
            _info.rectTransform.pivot = new Vector2(0, 0.5f);

            _minus = UIFactory.SignButton("Minus", content, false, bl, new Vector2(205, 48), 96, () => Press(-1));
            _workers = UIFactory.Label("Workers", content, "", 26, UIFactory.Brown, bl, new Vector2(320, 50), new Vector2(130, 76));
            _plus = UIFactory.SignButton("Plus", content, true, bl, new Vector2(435, 48), 96, () => Press(+1));
            _free = UIFactory.Text("Free", content, "", 24, UIFactory.Brown, TextAnchor.MiddleLeft, bl, new Vector2(510, 50), new Vector2(260, 76));
            _free.rectTransform.pivot = new Vector2(0, 0.5f);

            UIFactory.IconButton("Close", _panel, "UI/Icons/icon_close", "X", new Vector2(1, 1), new Vector2(-6, -6), new Vector2(84, 84), () => _close?.Invoke(), true);
            _panel.gameObject.SetActive(false);
            _m.Changed += Refresh;
        }

        void OnDestroy()
        {
            if (_m != null) _m.Changed -= Refresh;
        }

        void Press(int delta)
        {
            _move?.Invoke(delta);
            Refresh();
        }

        void Update()
        {
            if (_m == null) return;
            int n = _selected();
            if (n != _shown) Refresh();
        }

        /// <summary>Shows the selected building and its workers.</summary>
        public void Refresh()
        {
            int n = _selected();
            _shown = n;
            bool show = n >= 0 && n < Needs.Count && _m.Exists(n);
            UIFactory.SetActive(_panel, show);
            if (!show) return;

            var b = _m.B(n);
            var nc = _m.Cfg.Need(n);
            UIFactory.SetSprite(_icon, Art.Get(Art.NeedIcon(_m.Cfg, n)));
            UIFactory.SetActive(_minus, b.unlocked);
            UIFactory.SetActive(_plus, b.unlocked);
            UIFactory.SetActive(_workers, b.unlocked);
            UIFactory.SetActive(_free, b.unlocked);

            if (b.unlocked)
            {
                UIFactory.SetText(_title, nc.buildingName);
                UIFactory.SetText(_info, $"Serves {nc.displayName}   ·   Queue {_m.QueueCount(n)}");
                UIFactory.SetText(_workers, $"<size=22>Workers</size>\n{b.workers}");
                UIFactory.SetText(_free, _m.CanMoveWorkers
                    ? $"Free workers: {_m.FreeWorkers}\n<size=20>{(_m.MoveCost == 0 ? "moves are free" : $"move: {_m.MoveCost} coins")}</size>"
                    : "<color=#8A7F70>Move workers before the wave</color>");
                _minus.interactable = _m.CanMoveWorkers && b.workers > 0;
                _plus.interactable = _m.CanPullWorker(n);
            }
            else
            {
                var plot = _m.Plot(n);
                UIFactory.SetText(_title, $"{nc.buildingName}  <color=#8A7F70>locked</color>");
                UIFactory.SetText(_info, plot != null ? $"This area opens after wave {plot.unlockAfterWave}." : "Locked area.");
            }
        }
    }
}
