using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketIconInteractionState : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private Button _button;
        private Image _icon;
        private Color _normal;
        private Color _highlighted;
        private float _disabledAlpha;
        private bool _hovered;
        private bool _pressed;
        private bool _selected;
        private bool _stepperMode;
        private MarketStepperCellVisual _stepper;

        internal static void Attach(Button button, Color normal, Color highlighted, float disabledAlpha)
        {
            if (button == null || button.GetComponent<MarketIconInteractionState>() != null)
                return;
            Transform icon = button.transform.Find("Icon");
            Image image = icon == null ? null : icon.GetComponent<Image>();
            if (image == null)
                return;
            MarketIconInteractionState state = button.gameObject.AddComponent<MarketIconInteractionState>();
            state._button = button;
            state._icon = image;
            state._normal = normal;
            state._highlighted = highlighted;
            state._disabledAlpha = disabledAlpha;
            state.Apply();
        }

        internal static MarketIconInteractionState AttachStepper(Button button, UiMotion motion)
        {
            if (button == null)
                return null;
            Attach(button, UiFormatting.Ink, UiFormatting.Paper, 0.28f);
            MarketIconInteractionState state = button.GetComponent<MarketIconInteractionState>();
            if (state == null)
                return null;
            if (state._stepper == null)
            {
                state._stepper = new MarketStepperCellVisual(
                    button,
                    state._icon,
                    state._normal,
                    state._highlighted,
                    state._disabledAlpha,
                    motion);
            }
            state._stepperMode = true;
            state.Apply();
            return state;
        }

        internal static void Refresh(Button button)
        {
            if (button == null)
                return;
            MarketIconInteractionState state = button.GetComponent<MarketIconInteractionState>();
            if (state != null)
                state.Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left ||
                _button == null || !_button.IsInteractable())
                return;
            _pressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return;
            _pressed = false;
            Apply();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            Apply();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            Apply();
        }

        private void Update()
        {
            Apply();
            if (_stepperMode && _stepper != null)
                _stepper.Advance(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            _hovered = false;
            _pressed = false;
            _selected = false;
            if (_stepperMode && _stepper != null)
                _stepper.Reset(_button != null && _button.IsInteractable());
            else
                Apply();
        }

        private void Apply()
        {
            if (_button == null || _icon == null)
                return;
            bool interactable = _button.IsInteractable();
            if (_stepperMode && _stepper != null)
            {
                _stepper.Apply(
                    interactable && (_hovered || _pressed),
                    interactable && _pressed,
                    interactable);
                return;
            }
            Color color = interactable && (_hovered || _pressed || _selected) ? _highlighted : _normal;
            if (!interactable)
                color.a = _disabledAlpha;
            _icon.color = color;
        }
    }
}
