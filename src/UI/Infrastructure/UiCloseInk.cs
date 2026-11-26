using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Infrastructure
{
    // The sprite swap changes the paper under the cross. Tint its separate glyph in the same states.
    internal sealed class UiCloseInk : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        private Image _icon;
        private Button _button;
        private bool _hover, _focus;

        internal static void Attach(Button button)
        {
            Transform icon = button.transform.Find("Icon");
            if (icon == null || button.GetComponent<UiCloseInk>() != null) return;
            UiCloseInk state = button.gameObject.AddComponent<UiCloseInk>();
            state._button = button;
            state._icon = icon.GetComponent<Image>();
            SpriteState sprites = button.spriteState;
            sprites.selectedSprite = sprites.highlightedSprite;
            button.spriteState = sprites;
        }
        public void OnPointerEnter(PointerEventData e) { _hover = true; Paint(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; Paint(); }
        public void OnSelect(BaseEventData e) { _focus = !(e is PointerEventData); Paint(); }
        public void OnDeselect(BaseEventData e) { _focus = false; Paint(); }
        private void OnDisable() { _hover = _focus = false; Paint(); }
        private void Paint()
        {
            if (_icon != null)
                _icon.color = _button.IsInteractable() && (_hover || _focus) ? Color.white : UiFormatting.Ink;
        }
    }
}
