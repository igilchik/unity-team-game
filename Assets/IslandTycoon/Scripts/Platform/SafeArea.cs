using UnityEngine;

namespace IslandTycoon
{
    /// <summary>Keeps UI inside the safe area of the phone screen (notches, rounded corners, camera holes).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect _last;
        Vector2Int _lastSize;
        RectTransform _rt;

        void Awake() { _rt = (RectTransform)transform; Apply(); }

        void Update()
        {
            if (_last != Screen.safeArea || _lastSize.x != Screen.width || _lastSize.y != Screen.height) Apply();
        }

        void Apply()
        {
            var sa = Screen.safeArea;
            _last = sa;
            _lastSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _rt.anchorMin = new Vector2(sa.x / Screen.width, sa.y / Screen.height);
            _rt.anchorMax = new Vector2((sa.x + sa.width) / Screen.width, (sa.y + sa.height) / Screen.height);
            _rt.offsetMin = _rt.offsetMax = Vector2.zero;
        }
    }
}
