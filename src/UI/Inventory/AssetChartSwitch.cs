using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Market;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Inventory
{
    internal sealed class AssetChartSwitch : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private Image _frame, _paper, _arrows;
        private bool _pointed, _selected, _pressed;

        internal static Button Create(RectTransform header, UiFrontendServices services)
        {
            Image hit = InventoryPaper.Solid("SwitchAssetChart", header, Color.clear);
            hit.raycastTarget = true;
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            UiFocusOrigin.Attach(button);
            var state = hit.gameObject.AddComponent<AssetChartSwitch>();
            state._frame = InventoryPaper.Solid("SlantedFrame", hit.transform, UiFormatting.Ink);
            RankingLayout.Center(state._frame.rectTransform, 22f, 22f);
            state._frame.rectTransform.localRotation = Quaternion.Euler(0, 0, 5);
            Shadow shadow = state._frame.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(1.7f, -1.7f);
            shadow.effectColor = UiFormatting.Ink;
            UiPaperShape.Attach(state._frame);
            state._paper = InventoryPaper.Solid("Paper", state._frame.transform, UiFormatting.Paper);
            RankingLayout.Stretch(state._paper.rectTransform, 1.4f, 1.4f, 1.4f, 1.4f);
            UiPaperShape.Attach(state._paper);
            // The existing opposing-arrow trade mark reads as an exchange between the two charts.
            state._arrows = RankingLayout.Image("Icon", state._paper.transform, services.Assets.GetSprite("icon_trade"), UiFormatting.Ink);
            state._arrows.preserveAspect = true;
            RankingLayout.Center(state._arrows.rectTransform, 20, 20);
            state.Paint();
            return button;
        }

        public void OnPointerEnter(PointerEventData e) { _pointed = true; Paint(); }
        public void OnPointerExit(PointerEventData e) { _pointed = _pressed = false; Paint(); }
        public void OnPointerDown(PointerEventData e) { _selected = false; _pressed = e.button == PointerEventData.InputButton.Left; Paint(); }
        public void OnPointerUp(PointerEventData e) { _pressed = false; Paint(); }
        public void OnSelect(BaseEventData e) { _selected = !(e is PointerEventData); Paint(); }
        public void OnDeselect(BaseEventData e) { _selected = false; Paint(); }
        private void OnDisable() { _pointed = _selected = _pressed = false; Paint(); }

        private void Paint()
        {
            if (_paper == null || _arrows == null) return;
            bool highlighted = _pointed || _selected;
            _paper.color = _pressed ? UiFormatting.RedDark : highlighted ? UiFormatting.Ink : UiFormatting.Paper;
            _arrows.color = highlighted || _pressed ? UiFormatting.Paper : UiFormatting.Ink;
            _frame.rectTransform.anchoredPosition = _pressed ? new Vector2(1, -1) : Vector2.zero;
        }
    }
}
