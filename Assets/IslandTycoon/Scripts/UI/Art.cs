using System.Collections.Generic;
using UnityEngine;

namespace IslandTycoon
{
    /// <summary>Loads sprites from Resources/IslandTycoon/Sprites with a cache and graceful fallbacks.</summary>
    public static class Art
    {
        const string Root = "IslandTycoon/Sprites/";
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static Font _font;

        /// <summary>Returns null without a warning when the sprite does not exist (optional art).</summary>
        public static Sprite TryGet(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Cache.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(Root + path);
            if (s == null)
            {
                // texture imported without the Sprite type: build one at runtime
                var tex = Resources.Load<Texture2D>(Root + path);
                if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            Cache[path] = s;
            return s;
        }

        public static Sprite Get(string path)
        {
            var s = TryGet(path);
            if (s == null) Debug.LogWarning("[IslandTycoon] Missing sprite: " + path);
            return s;
        }

        /// <summary>First existing sprite of the list.</summary>
        public static Sprite First(params string[] paths)
        {
            foreach (var p in paths) { var s = TryGet(p); if (s != null) return s; }
            return null;
        }

        /// <summary>Game font (Fredoka One) with built-in fallbacks.</summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("IslandTycoon/Fonts/FredokaOne");
                if (_font != null) return _font;
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (_font == null) try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                return _font;
            }
        }
    }
}
