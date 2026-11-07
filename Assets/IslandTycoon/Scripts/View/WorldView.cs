using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IslandTycoon
{
    /// <summary>Positions on the island, measured in background pixels (100 px per unit).</summary>
    public static class Layout
    {
        public static float BgW = 16.72f, BgH = 9.41f;
        static Vector2 _px = new Vector2(1672, 941);
        static IslandConfig _ic;
        static readonly Dictionary<int, PlotConfig> Plots = new Dictionary<int, PlotConfig>();

        public static void Configure(IslandConfig ic, Vector2 bgPixels)
        {
            _ic = ic;
            _px = bgPixels;
            BgW = bgPixels.x / 100f;
            BgH = bgPixels.y / 100f;
            Plots.Clear();
            foreach (var p in ic.plots) Plots[(int)p.need] = p;
        }

        /// <summary>Background pixel (x right, y down) to a world position centred on the island.</summary>
        public static Vector2 Px(float x, float y) => new Vector2((x - _px.x * 0.5f) / 100f, (_px.y * 0.5f - y) / 100f);
        public static Vector2 Px(Vector2 p) => Px(p.x, p.y);

        public static bool HasPlot(int need) => Plots.ContainsKey(need);

        /// <summary>Bottom corner of the iso plot: buildings stand on it.</summary>
        public static Vector2 PlotBase(int need)
        {
            var p = Plots[need];
            return Px(p.pixel.x, p.pixel.y + p.width * 0.25f);
        }

        public static float PlotWidth(int need) => Plots[need].width / 100f;

        /// <summary>Workers of a building stand in a row next to it.</summary>
        public static Vector2 WorkerSpot(int need, int i)
        {
            float w = PlotWidth(need);
            return PlotBase(need) + new Vector2(-w * 0.36f - i * 0.38f, 0.22f - i * 0.04f);
        }

        /// <summary>Free workers wait in a small group near the pier.</summary>
        public static Vector2 FreeWorkerSpot(int i) =>
            Px(_ic.freeWorkersPixel.x + (i % 4) * 45, _ic.freeWorkersPixel.y + (i / 4) * 30 + (i % 2) * 10);

        /// <summary>Lower on screen = drawn in front.</summary>
        public static int Order(float y, int bias = 0) => 5000 - Mathf.RoundToInt(y * 100f) + bias;
    }

    /// <summary>Drag to pan, wheel / pinch to zoom; forwarded from anything on the island.</summary>
    public abstract class IslandTouch : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public GameController Controller;
        public void OnBeginDrag(PointerEventData e) => Controller.Rig.Begin(e);
        public void OnDrag(PointerEventData e) => Controller.Rig.Drag(e);
        public void OnEndDrag(PointerEventData e) => Controller.Rig.End(e);
        public void OnScroll(PointerEventData e) => Controller.Rig.Scroll(e);
    }

    /// <summary>Island art and the sea around it. A tap on empty island closes the building panel.</summary>
    public class IslandBackground : IslandTouch, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e) => Controller.SelectBuilding(-1);
    }

    /// <summary>One building plot on the island; tap to select it.</summary>
    public class PlotView : IslandTouch, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e) => Controller.SelectBuilding(Need);

        public int Need;
        public SpriteRenderer Body, Warning;
        public readonly List<Transform> Workers = new List<Transform>();
        public float Top = 2f;
    }

    /// <summary>
    /// SCRUM-15: Home / Island View. Renders the resort island; holds no game rules.
    /// SCRUM-16: shows every building plot, open buildings and locked areas.
    /// SCRUM-46: a pulsing warning sign above a building with a bottleneck.
    /// SCRUM-75: the selected building gently pulses.
    /// SCRUM-78: workers stand next to their building, free workers by the pier; every move shows at once.
    /// </summary>
    public class WorldView : MonoBehaviour
    {
        GameController _c;
        GameModel M => _c.Model;
        Transform _content;
        readonly Dictionary<int, PlotView> _plots = new Dictionary<int, PlotView>();
        readonly List<Transform> _freeWorkers = new List<Transform>();
        const float WorkerHeight = 1.25f;

        public void Build(GameController c)
        {
            _c = c;
            Rebuild();
        }

        /// <summary>(Re)creates everything for the island.</summary>
        public void Rebuild()
        {
            if (_content != null) Destroy(_content.gameObject);
            _plots.Clear();
            _freeWorkers.Clear();
            _content = new GameObject("Island").transform;
            _content.SetParent(transform, false);

            var ic = M.IC;
            var bgSprite = Art.Get(ic.background);
            var size = bgSprite != null ? bgSprite.rect.size : ic.backgroundPixels;
            Layout.Configure(ic, size);

            var bg = Art.MakeSprite("Background", _content, null, -10000);
            bg.sprite = bgSprite;
            Art.FitCenter(bg, Layout.BgH);
            bg.gameObject.AddComponent<BoxCollider2D>();
            bg.gameObject.AddComponent<IslandBackground>().Controller = _c;

            // Sea around the art, visible when the camera pans or zooms out: left/right = mirrored island,
            // top/bottom = open-water strips made from the art's own edge colours.
            if (bgSprite != null)
            {
                string key = System.IO.Path.GetFileName(ic.background);
                var top = Art.TryGet("Backgrounds/sea_top_" + key);
                var bottom = Art.TryGet("Backgrounds/sea_bottom_" + key);
                float k = bg.transform.localScale.x;
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx != 0) AddSea(bgSprite, true, new Vector2(dx * Layout.BgW, 0), k);
                    if (top != null) AddSea(top, dx != 0, new Vector2(dx * Layout.BgW, (Layout.BgH + top.rect.height / 100f * k) * 0.5f), k);
                    if (bottom != null) AddSea(bottom, dx != 0, new Vector2(dx * Layout.BgW, -(Layout.BgH + bottom.rect.height / 100f * k) * 0.5f), k);
                }
            }
            _c.Rig.Reset();

            for (int n = 0; n < Needs.Count; n++)
                if (Layout.HasPlot(n) && M.Exists(n)) _plots[n] = BuildPlot(n);
        }

        PlotView BuildPlot(int need)
        {
            var root = new GameObject("Plot_" + (NeedType)need);
            root.transform.SetParent(_content, false);
            root.transform.localPosition = Layout.PlotBase(need);
            var pv = root.AddComponent<PlotView>();
            pv.Need = need;
            pv.Controller = _c;
            pv.Body = Art.MakeSprite("Body", root.transform, null, Layout.Order(Layout.PlotBase(need).y));
            pv.Warning = Art.MakeSprite("Warning", root.transform, "UI/Icons/icon_warning", 9000);
            pv.Warning.gameObject.SetActive(false);
            RefreshPlotArt(pv);
            return pv;
        }

        void RefreshPlotArt(PlotView pv)
        {
            var b = M.B(pv.Need);
            var plot = M.Plot(pv.Need);
            pv.Body.sprite = Art.Building(M.Cfg, pv.Need, b.unlocked, plot != null && plot.jungleLock);
            float width = Layout.PlotWidth(pv.Need) * 1.08f;
            Art.FitBottom(pv.Body, width, -0.06f);
            // locked areas look a bit faded
            pv.Body.color = b.unlocked ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            var old = pv.Body.GetComponent<BoxCollider2D>();
            if (old != null) Destroy(old);
            pv.Body.gameObject.AddComponent<BoxCollider2D>();
            pv.Top = pv.Body.sprite != null ? pv.Body.sprite.bounds.size.y * pv.Body.transform.localScale.y : 2f;
        }

        void LateUpdate()
        {
            if (_c == null || M == null) return;
            float t = Time.time;
            foreach (var pv in _plots.Values) SyncPlot(pv, t);
            SyncFreeWorkers(t);
        }

        void SyncPlot(PlotView pv, float t)
        {
            float sel = _c.Selected == pv.Need ? 1f + Mathf.Sin(t * 5f) * 0.015f : 1f;
            pv.transform.localScale = new Vector3(sel, sel, 1);

            bool bottleneck = M.IsBottleneck(pv.Need);
            pv.Warning.gameObject.SetActive(bottleneck);
            if (bottleneck && pv.Warning.sprite != null)
            {
                float k = 0.7f / Mathf.Max(0.01f, pv.Warning.sprite.bounds.size.y) * (1f + Mathf.Sin(t * 8f) * 0.1f);
                pv.Warning.transform.localScale = new Vector3(k, k, 1);
                pv.Warning.transform.localPosition = new Vector3(0, pv.Top + 0.3f, 0);
            }

            // assigned workers stand in front of their building
            var b = M.B(pv.Need);
            int want = b.unlocked ? b.workers : 0;
            while (pv.Workers.Count < want)
            {
                var holder = new GameObject("Worker").transform;
                holder.SetParent(_content, false);
                holder.localPosition = Layout.WorkerSpot(pv.Need, pv.Workers.Count);
                var w = Art.MakeSprite("Body", holder, "Characters/Worker/worker_front", 0);
                Art.FitFeet(w, WorkerHeight);
                pv.Workers.Add(holder);
                _c.Fx("FX/fx_puff", holder.position + Vector3.up * 0.4f, 0.7f, 0.1f);
            }
            while (pv.Workers.Count > want)
            {
                var h = pv.Workers[pv.Workers.Count - 1];
                pv.Workers.RemoveAt(pv.Workers.Count - 1);
                _c.Fx("FX/fx_puff", h.position + Vector3.up * 0.4f, 0.7f, 0.1f);
                Destroy(h.gameObject);
            }
            int baseOrder = Layout.Order(Layout.PlotBase(pv.Need).y);
            bool busy = M.InServiceCount(pv.Need) > 0;
            for (int i = 0; i < pv.Workers.Count; i++)
            {
                var h = pv.Workers[i];
                h.localPosition = Layout.WorkerSpot(pv.Need, i) + new Vector2(0, busy ? Mathf.Abs(Mathf.Sin(t * 9f + i)) * 0.05f : 0);
                h.GetComponentInChildren<SpriteRenderer>().sortingOrder = baseOrder + 5 + i;
            }
        }

        void SyncFreeWorkers(float t)
        {
            int want = Mathf.Max(0, M.FreeWorkers);
            while (_freeWorkers.Count < want)
            {
                var holder = new GameObject("FreeWorker").transform;
                holder.SetParent(_content, false);
                var spot = Layout.FreeWorkerSpot(_freeWorkers.Count);
                holder.localPosition = spot;
                var w = Art.MakeSprite("Body", holder, "Characters/Worker/worker_wave", Layout.Order(spot.y, 1));
                Art.FitFeet(w, WorkerHeight);
                _freeWorkers.Add(holder);
                _c.Fx("FX/fx_puff", holder.position + Vector3.up * 0.4f, 0.7f, 0.1f);
            }
            while (_freeWorkers.Count > want)
            {
                var h = _freeWorkers[_freeWorkers.Count - 1];
                _freeWorkers.RemoveAt(_freeWorkers.Count - 1);
                Destroy(h.gameObject);
            }
            for (int i = 0; i < _freeWorkers.Count; i++)
                _freeWorkers[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2f + i) * 3f);
        }

        void AddSea(Sprite sprite, bool mirror, Vector2 center, float scale)
        {
            var m = Art.MakeSprite("Sea", _content, null, -10001);
            m.sprite = sprite;
            m.flipX = mirror;
            var b = sprite.bounds;
            m.transform.localScale = new Vector3(scale, scale, 1);
            m.transform.localPosition = (Vector3)center - new Vector3(b.center.x * scale * (mirror ? -1 : 1), b.center.y * scale, 0);
            m.gameObject.AddComponent<BoxCollider2D>();
            m.gameObject.AddComponent<IslandBackground>().Controller = _c;
        }
    }
}
