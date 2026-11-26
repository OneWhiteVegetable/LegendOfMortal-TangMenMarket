using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketCheckoutInk : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private enum Pose { Rest, Hover, Pressed, Disabled }
        private static readonly Color HoverColor = new Color32(198, 77, 64, 255);
        private Button _button;
        private UiFrontendServices _services;
        private RectTransform _paper;
        private Image _face;
        private Shadow _shadow;
        private CanvasGroup _visibility;
        private bool _hovered;
        private bool _focused;
        private bool _pressed;
        private Pose _pose;
        private Vector2 _fromPosition;
        private Color _fromColor;
        private float _elapsed;

        internal static MarketCheckoutInk Attach(Button button, Image icon, UiFrontendServices services)
        {
            var ink = button.gameObject.AddComponent<MarketCheckoutInk>();
            ink._button = button;
            ink._services = services;
            Text label = button.GetComponentInChildren<Text>(true);
            Image hit = button.GetComponent<Image>();
            button.transition = Selectable.Transition.None;
            button.spriteState = new SpriteState();
            hit.overrideSprite = hit.sprite = null;
            hit.type = Image.Type.Simple;
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Shadow originalShadow = button.GetComponent<Shadow>();
            if (originalShadow != null) originalShadow.enabled = false;

            Image border = Panel("CheckoutPaper", button.transform, 0f, new Color32(208, 113, 96, 255));
            ink._paper = border.rectTransform;
            ink._visibility = border.gameObject.AddComponent<CanvasGroup>();
            ink._shadow = border.gameObject.AddComponent<Shadow>();
            ink._shadow.effectColor = Color.black;
            ink._shadow.effectDistance = new Vector2(4f, -4f);
            ink._face = Panel("RedFace", border.transform, 1f, UiFormatting.Red);
            label.transform.SetParent(ink._paper, false);
            icon.transform.SetParent(ink._paper, false);
            label.color = icon.color = UiFormatting.Paper;
            ink.Paint(true);
            return ink;
        }

        private static Image Panel(string name, Transform parent, float inset, Color color)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            image.rectTransform.SetParent(parent, false);
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = new Vector2(inset, inset);
            image.rectTransform.offsetMax = -image.rectTransform.offsetMin;
            image.raycastTarget = false;
            image.color = color;
            return image;
        }

        public void OnPointerEnter(PointerEventData e) { _hovered = true; }
        public void OnPointerExit(PointerEventData e) { _hovered = _pressed = false; }
        public void OnPointerDown(PointerEventData e) { _pressed = e.button == PointerEventData.InputButton.Left; }
        public void OnPointerUp(PointerEventData e) { _pressed = false; }
        public void OnSelect(BaseEventData e) { _focused = !(e is PointerEventData); }
        public void OnDeselect(BaseEventData e) { _focused = false; }
        private void OnDisable() { _hovered = _focused = _pressed = false; }
        private void LateUpdate() { Paint(false); }

        internal void Paint(bool immediate)
        {
            if (_button == null) return;
            bool modalOpen = _services.Overlays.HasModal;
            if (modalOpen || !_button.IsInteractable()) _pressed = false;
            bool highlighted = !modalOpen && (_hovered || (_focused && UiFocusOrigin.KeyboardActive));
            Pose next = !_button.IsInteractable() ? Pose.Disabled : _pressed ? Pose.Pressed : highlighted ? Pose.Hover : Pose.Rest;
            if (_pose != next || immediate)
            {
                _fromPosition = _paper.anchoredPosition;
                _fromColor = _face.color;
                _elapsed = 0f;
                _pose = next;
            }
            _elapsed = immediate || _services.Motion.ReducedMotion ? .12f : Mathf.Min(.12f, _elapsed + Time.unscaledDeltaTime);
            float t = _elapsed >= .12f ? 1f : UiMotionEasing.CubicBezier(_elapsed / .12f, .25f, .1f, .25f, 1f);
            Vector2 target = next == Pose.Pressed ? new Vector2(2f, -2f) :
                next == Pose.Hover ? new Vector2(-1f, 2f) : Vector2.zero;
            _paper.anchoredPosition = Vector2.LerpUnclamped(_fromPosition, target, t);
            _face.color = Color.LerpUnclamped(_fromColor, next == Pose.Hover || next == Pose.Pressed ? HoverColor : UiFormatting.Red, t);
            _shadow.enabled = next != Pose.Pressed && next != Pose.Disabled;
            _visibility.alpha = next == Pose.Disabled ? .35f : 1f;
        }
    }
}
