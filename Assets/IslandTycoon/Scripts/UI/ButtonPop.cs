using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IslandTycoon
{
    /// <summary>Tiny press animation for instant feedback on every button.</summary>
    public class ButtonPop : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        /// <summary>Raised on every button press (e.g. for a click sound).</summary>
        public static event Action Pressed;

        float _target = 1f;

        public void OnPointerDown(PointerEventData e)
        {
            _target = 0.92f;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;

        void Update()
        {
            float s = Mathf.MoveTowards(transform.localScale.x, _target, Time.unscaledDeltaTime * 3f);
            transform.localScale = new Vector3(s, s, 1);
        }
    }
}
