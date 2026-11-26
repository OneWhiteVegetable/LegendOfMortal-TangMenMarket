using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketDetailActionFace : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private Button _button;
        private UiMotion _motion;
        private Image _border;
        private Image _face;
        private Shadow _shadow;
        private Text _label;
        private Image _icon;
        private CanvasGroup _group;
        private Color _color;
        private bool _opening;
        private bool _hover;
        private bool _focus;
        private bool _press;

        internal static void Attach(Button button, Color color, bool opening, UiMotion motion)
        {
            var skin = button.gameObject.AddComponent<MarketDetailActionFace>();
            skin._button = button;
            skin._color = color;
            skin._opening = opening;
            skin._motion = motion;
            button.transition = Selectable.Transition.None;
            Image hit = button.GetComponent<Image>();
            hit.overrideSprite = null;
            hit.sprite = null;
            hit.color = Color.clear;
            hit.raycastTarget = true;
            skin._border = Panel("PaperFace", button.transform, 0f);
            skin._group = skin._border.gameObject.AddComponent<CanvasGroup>();
            if (opening)
            {
                skin._shadow = skin._border.gameObject.AddComponent<Shadow>();
                skin._shadow.effectColor = Color.black;
                skin._shadow.effectDistance = new Vector2(3f, -3f);
            }
            skin._face = Panel("Inset", skin._border.transform, 1f);
            skin._label = button.transform.Find("Label").GetComponent<Text>();
            skin._icon = button.transform.Find("ActionIcon").GetComponent<Image>();
            skin._icon.rectTransform.SetParent(skin._face.transform, false);
            skin._label.rectTransform.SetParent(skin._face.transform, false);
            skin._label.lineSpacing = 1f;
            skin._label.fontSize = opening ? 13 : 11;
            skin._label.fontStyle = opening ? FontStyle.Bold : FontStyle.Normal;
            skin._label.alignment = TextAnchor.MiddleLeft;
            skin._label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var layout = skin._face.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = opening ? 8f : 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            LayoutElement iconLayout = skin._icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = iconLayout.preferredHeight = opening ? 24f : 16f;
            LayoutElement labelLayout = skin._label.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = Mathf.Ceil(skin._label.preferredWidth);
            labelLayout.preferredHeight = opening ? 42f : 30f;
            skin.Paint(true);
        }

        private static Image Panel(string name, Transform parent, float inset)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            image.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = -rect.offsetMin;
            image.raycastTarget = false;
            return image;
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; }
        public void OnPointerExit(PointerEventData e) { _hover = _press = false; }
        public void OnPointerDown(PointerEventData e) { _press = e.button == PointerEventData.InputButton.Left; }
        public void OnPointerUp(PointerEventData e) { _press = false; }
        public void OnSelect(BaseEventData e) { _focus = !(e is PointerEventData); }
        public void OnDeselect(BaseEventData e) { _focus = false; }

        private void OnDisable() { _press = _hover = _focus = false; }
        private void LateUpdate() { Paint(false); }

        private void Paint(bool immediate)
        {
            bool enabled = _button.IsInteractable();
            bool highlighted = enabled && (_hover || _focus);
            bool pressed = enabled && _press;
            _group.alpha = enabled ? 1f : 0.3f;
            if (_shadow != null)
                _shadow.enabled = enabled && !pressed;
            Color color = _color;
            if (highlighted && _opening)
                color = new Color(color.r * 1.13f, color.g * 1.13f, color.b * 1.13f);
            _face.color = color;
            _border.color = _opening ? Color.Lerp(color, Color.white, 0.24f) :
                highlighted ? UiFormatting.Gold : (Color)new Color32(89, 98, 87, 255);
            _label.color = _icon.color = _opening ? UiFormatting.Paper :
                highlighted ? UiFormatting.Gold : (Color)new Color32(196, 201, 189, 255);
            Vector2 target = !_opening ? Vector2.zero : pressed ? new Vector2(2f, -2f) :
                highlighted ? new Vector2(0f, 2f) : Vector2.zero;
            _border.rectTransform.anchoredPosition = immediate || _motion.ReducedMotion ? target :
                Vector2.MoveTowards(_border.rectTransform.anchoredPosition, target, Time.unscaledDeltaTime * 40f);
        }
    }
}
