using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketCartClearMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private static readonly Color Edge = new Color32(89, 98, 87, 255);
        private static readonly Color Icon = new Color32(174, 181, 170, 255);
        private Button _button;
        private UiMotion _motion;
        private Image _face;
        private Image _icon;
        private RectTransform _paper;
        private CanvasGroup _opacity;
        private bool _hovered;
        private bool _focused;
        private bool _pressed;
        private float _progress;

        internal static void Attach(Button button, UiMotion motion)
        {
            var driver = button.gameObject.AddComponent<MarketCartClearMotion>();
            driver._button = button;
            driver._motion = motion;
            button.transition = Selectable.Transition.None;
            button.targetGraphic.color = Color.clear;
            button.targetGraphic.canvasRenderer.SetColor(Color.white);
            Image border = Panel("Paper", button.transform, 0f, Edge);
            border.gameObject.AddComponent<MarketPaperEdge>();
            driver._paper = border.rectTransform;
            driver._face = Panel("Face", border.transform, 1f, UiFormatting.Ink);
            driver._icon = button.transform.Find("Icon").GetComponent<Image>();
            driver._icon.rectTransform.SetParent(border.transform, false);
            driver._icon.raycastTarget = false;
            driver._opacity = border.gameObject.AddComponent<CanvasGroup>();
            driver.Paint();
        }

        private static Image Panel(string name, Transform parent, float inset, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = -rect.offsetMin;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public void OnPointerEnter(PointerEventData e) { _hovered = true; }
        public void OnPointerExit(PointerEventData e) { _hovered = false; _pressed = false; }
        public void OnSelect(BaseEventData e) { _focused = true; }
        public void OnDeselect(BaseEventData e) { _focused = false; }
        public void OnPointerDown(PointerEventData e) { _pressed = e.button == PointerEventData.InputButton.Left; }
        public void OnPointerUp(PointerEventData e) { _pressed = false; }

        private void OnDisable()
        {
            _pressed = _hovered = _focused = false;
            _progress = 0f;
        }

        private void Update() { Paint(); }

        private void Paint()
        {
            bool enabled = _button.IsInteractable();
            float target = enabled && (_hovered || (_focused && UiFocusOrigin.KeyboardActive)) ? 1f : 0f;
            _progress = _motion.ReducedMotion ? target : Mathf.MoveTowards(_progress, target, Time.unscaledDeltaTime / 0.12f);
            _face.color = Color.Lerp(UiFormatting.Ink, UiFormatting.Red, _progress);
            _icon.color = Color.Lerp(Icon, UiFormatting.Paper, _progress);
            _opacity.alpha = enabled ? 1f : 0.25f;
            _paper.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-3f, 4f, _progress));
            _paper.localScale = Vector3.one * (enabled && _pressed ? 0.96f : Mathf.Lerp(1f, 1.06f, _progress));
        }
    }
}
