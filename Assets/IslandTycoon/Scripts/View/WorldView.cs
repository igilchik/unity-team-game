using UnityEngine;
using UnityEngine.EventSystems;

namespace IslandTycoon
{
    /// <summary>Positions on the island, measured in background pixels (100 px per unit).</summary>
    public static class Layout
    {
        public static float BgW = 16.72f, BgH = 9.41f;
        static Vector2 _px = new Vector2(1672, 941);

        public static void Configure(IslandConfig ic, Vector2 bgPixels)
        {
            _px = bgPixels;
            BgW = bgPixels.x / 100f;
            BgH = bgPixels.y / 100f;
        }

        /// <summary>Background pixel (x right, y down) to a world position centred on the island.</summary>
        public static Vector2 Px(float x, float y) => new Vector2((x - _px.x * 0.5f) / 100f, (_px.y * 0.5f - y) / 100f);
        public static Vector2 Px(Vector2 p) => Px(p.x, p.y);

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

    /// <summary>Island art and the sea around it.</summary>
    public class IslandBackground : IslandTouch { }

    /// <summary>
    /// SCRUM-15: Home / Island View. Renders the resort island; holds no game rules.
    /// </summary>
    public class WorldView : MonoBehaviour
    {
        GameController _c;
        GameModel M => _c.Model;
        Transform _content;

        public void Build(GameController c)
        {
            _c = c;
            Rebuild();
        }

        /// <summary>(Re)creates everything for the island.</summary>
        public void Rebuild()
        {
            if (_content != null) Destroy(_content.gameObject);
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
