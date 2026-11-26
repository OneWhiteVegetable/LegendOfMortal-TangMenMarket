using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketBrushArrow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private Button _button;
        private Image _icon;
        private RectTransform _paper;
        private UiMotion _motion;
        private float _direction, _progress;
        private bool _hovered, _focused, _pressed;
        private float _lastRevealed = -1f, _lastPressScale;
        private bool _lastInteractable;

        internal static void Attach(Button button, UiMotion motion, bool right = false)
        {
            Image surface = button.image;
            Image icon = button.transform.Find("Icon").GetComponent<Image>();
            var state = button.gameObject.AddComponent<MarketBrushArrow>();
            state._button = button;
            state._motion = motion;
            state._icon = icon;
            state._direction = right ? 1f : -1f;
            Image paper = RankingLayout.Image("BrushPaper", button.transform,
                button.spriteState.highlightedSprite, UiFormatting.Red);
            paper.rectTransform.SetAsFirstSibling();
            RankingLayout.Stretch(paper.rectTransform, -3f, 1f, -3f, 1f);
            state._paper = paper.rectTransform;
            button.transition = Selectable.Transition.None;
            surface.overrideSprite = null;
            surface.sprite = null;
            surface.color = Color.clear;
            surface.raycastTarget = true;
            state.Apply(button.IsInteractable(), 0f);
        }

        public void OnPointerEnter(PointerEventData data) { _hovered = true; }
        public void OnPointerExit(PointerEventData data) { _hovered = _pressed = false; }
        public void OnPointerDown(PointerEventData data) { _pressed = data.button == PointerEventData.InputButton.Left; }
        public void OnPointerUp(PointerEventData data) { _pressed = false; }
        public void OnSelect(BaseEventData data) { _focused = !(data is PointerEventData); }
        public void OnDeselect(BaseEventData data) { _focused = false; }

        private void OnDisable()
        {
            _hovered = _focused = _pressed = false;
            _progress = 0f;
            if (_button != null) Apply(_button.IsInteractable(), 0f);
        }

        private void Update()
        {
            bool enabled = _button.IsInteractable();
            float target = enabled && (_hovered || (_focused && UiFocusOrigin.KeyboardActive)) ? 1f : 0f;
            // The first frame after opening a detail can include the complete cost of opening it.
            float step = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) / 0.15f;
            _progress = _motion.ReducedMotion ? target : Mathf.MoveTowards(_progress, target, step);
            Apply(enabled, 1f - Mathf.Pow(1f - _progress, 3f));
        }

        private void Apply(bool interactable, float revealed)
        {
            float pressScale = interactable && _pressed ? 0.9f : 1f;
            if (_lastRevealed == revealed && _lastPressScale == pressScale && _lastInteractable == interactable)
                return;
            _lastRevealed = revealed;
            _lastPressScale = pressScale;
            _lastInteractable = interactable;
            _paper.localScale = new Vector3(revealed * pressScale, pressScale, 1f);
            _icon.rectTransform.anchoredPosition = new Vector2(2f * _direction * revealed, 0f);
            _icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f * _direction * revealed);
            _icon.rectTransform.localScale = Vector3.one * pressScale;
            Color color = Color.Lerp(UiFormatting.Ink, UiFormatting.Paper, revealed);
            if (!interactable) color.a = 0.2f;
            _icon.color = color;
        }
    }
}
