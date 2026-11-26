using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiModalButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private UiMotion _motion;
        private UiControlInkOffset[] _graphics;
        private bool _hover, _focus, _pressed;
        private float _height;

        internal void Configure(UiMotion motion)
        {
            _motion = motion;
            Graphic[] graphics = GetComponentsInChildren<Graphic>();
            _graphics = new UiControlInkOffset[graphics.Length];
            for (int i = 0; i < graphics.Length; i++)
                _graphics[i] = graphics[i].gameObject.AddComponent<UiControlInkOffset>();
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; }
        public void OnPointerExit(PointerEventData e) { _hover = false; }
        public void OnPointerDown(PointerEventData e) { _pressed = true; }
        public void OnPointerUp(PointerEventData e) { _pressed = false; }
        public void OnSelect(BaseEventData e) { _focus = !(e is PointerEventData); }
        public void OnDeselect(BaseEventData e) { _focus = false; }

        private void LateUpdate()
        {
            float target = (_hover || _focus) && !_pressed ? 2f : 0f;
            if (_height == target)
                return;
            _height = _motion.ReducedMotion ? target : Mathf.MoveTowards(_height, target, Time.unscaledDeltaTime * 20f);
            foreach (UiControlInkOffset graphic in _graphics)
                graphic.Set(Vector2.up * _height);
        }
    }
}
