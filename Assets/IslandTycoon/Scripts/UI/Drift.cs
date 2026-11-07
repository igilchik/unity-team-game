using UnityEngine;

namespace IslandTycoon
{
    /// <summary>Gentle idle motion for UI: x = scale pulse, y = vertical bob (px), z = period (s).</summary>
    public class Drift : MonoBehaviour
    {
        public Vector3 Amount;
        Vector2 _base;
        RectTransform _rt;

        void Start() { _rt = (RectTransform)transform; _base = _rt.anchoredPosition; }

        void Update()
        {
            if (_rt == null) return;
            float t = Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / Mathf.Max(0.1f, Amount.z));
            _rt.anchoredPosition = _base + new Vector2(0, t * Amount.y);
            float k = 1f + (t * 0.5f + 0.5f) * Amount.x;
            _rt.localScale = new Vector3(k, k, 1);
        }
    }
}
