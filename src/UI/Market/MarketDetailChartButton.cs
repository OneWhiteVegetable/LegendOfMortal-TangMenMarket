using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketDetailChartButton : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private const float TabWidth = 42f;
        private const float ResetWidth = 49f;
        private Image _background;
        private Image _inset;
        private Image _icon;
        private Text _label;
        private bool _selected, _pointer, _keyboard;

        internal static MarketDetailChartButton Attach(Button button, int index)
        {
            var skin = button.gameObject.AddComponent<MarketDetailChartButton>();
            bool reset = index < 0;
            button.transition = Selectable.Transition.None;
            skin._background = button.GetComponent<Image>();
            UiPaperShape.Attach(skin._background, reset ? 0f : 2.2f);
            skin._background.canvasRenderer.SetColor(Color.white);
            skin._label = button.transform.Find("Label").GetComponent<Text>();
            skin._label.fontStyle = FontStyle.Normal;
            skin._label.fontSize = reset ? 10 : 11;
            skin._label.alignment = TextAnchor.MiddleCenter;
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(reset ? ResetWidth : TabWidth, 25f);
            rect.anchoredPosition = new Vector2(reset ? 0f : -(ResetWidth + 10f + (3 - index) * (TabWidth + 3f)), 0f);
            if (reset)
            {
                skin._icon = button.transform.Find("Icon").GetComponent<Image>();
                RectTransform icon = skin._icon.rectTransform;
                icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                icon.pivot = new Vector2(0f, 0.5f);
                icon.sizeDelta = new Vector2(13f, 13f);
                icon.anchoredPosition = new Vector2(4f, 0f);
                UiView.Stretch(skin._label.rectTransform);
                skin._label.rectTransform.offsetMin = new Vector2(18f, 0f);
            }
            else
            {
                skin._inset = RankingLayout.Image("Inset", rect, null, Color.clear);
                skin._inset.rectTransform.SetAsFirstSibling();
                UiView.Stretch(skin._inset.rectTransform);
                skin._inset.rectTransform.offsetMin = Vector2.one;
                skin._inset.rectTransform.offsetMax = -Vector2.one;
                UiPaperShape.Attach(skin._inset, 2.2f);
            }
            skin.Paint();
            return skin;
        }

        internal void SetSelected(bool selected) { _selected = selected; Paint(); }
        public void OnPointerEnter(PointerEventData e) { _pointer = true; Paint(); }
        public void OnPointerExit(PointerEventData e) { _pointer = false; Paint(); }
        public void OnSelect(BaseEventData e) { _keyboard = !(e is PointerEventData); Paint(); }
        public void OnDeselect(BaseEventData e) { _keyboard = false; Paint(); }
        private void OnDisable() { _pointer = _keyboard = false; Paint(); }

        private void Paint()
        {
            if (_label == null)
                return;
            bool hover = _pointer || _keyboard;
            if (_icon != null)
            {
                _background.color = Color.clear;
                _label.color = _icon.color = hover ? UiFormatting.Red : UiFormatting.Muted;
                return;
            }
            _background.color = hover ? UiFormatting.Gold : _selected ? UiFormatting.Ink : Color.clear;
            _inset.color = _selected ? UiFormatting.Ink : hover ? UiFormatting.Paper : Color.clear;
            _label.color = _selected ? UiFormatting.Paper : hover ? UiFormatting.Ink : UiFormatting.Muted;
        }
    }
}
