using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Market;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Inventory
{
    internal sealed class InventoryCloseAllStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private Button _button;
        private Image _icon;
        private Text _label;
        private Color _ink;
        private bool _hover, _focus;

        internal static void Attach(Button button, UiFrontendServices services, bool isLong)
        {
            var style = button.gameObject.AddComponent<InventoryCloseAllStyle>();
            style._button = button;
            style._ink = isLong ? UiFormatting.Red : UiFormatting.Green;
            style._label = button.transform.Find("Label").GetComponent<Text>();
            style._label.fontSize = 10;
            style._label.font = services.Assets.BoldFont;
            button.transition = Selectable.Transition.None;
            InventoryPaper.Face(button.image, Color.clear);
            InventoryPaper.Right(button.image.rectTransform,113,0,74,26);
            InventoryPaper.Frame(button.transform,style._ink,1,style._ink);
            style._icon = RankingLayout.Image("ActionIcon",button.transform,services.Assets.GetSprite(isLong?"icon_trade":"icon_return"),style._ink);
            InventoryPaper.Box(style._icon.rectTransform,8,6,14,14);
            InventoryPaper.Box(style._label.rectTransform,24,0,47,26);
            style.Paint();
        }
        public void OnPointerEnter(PointerEventData e) { _hover = true; Paint(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; Paint(); }
        public void OnSelect(BaseEventData e) { _focus = !(e is PointerEventData); Paint(); }
        public void OnDeselect(BaseEventData e) { _focus = false; Paint(); }
        private void OnDisable() { _focus = _hover = false; if (_button != null) Paint(); }
        internal void Paint()
        {
            bool active = (_hover || _focus) && _button.interactable;
            _button.image.color = active ? _ink : Color.clear;
            Color text = active ? UiFormatting.Paper : _ink;
            if (!_button.interactable) text.a = 0.4f;
            _label.color = _icon.color = text;
        }
    }
}
