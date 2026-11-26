using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    internal static class UiView
    {
        internal static void SetLabel(Button button, string value)
        {
            if (button == null)
                return;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = value ?? string.Empty;
        }

        internal static void SetLabelColor(Button button, Color color)
        {
            if (button == null)
                return;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.color = color;
        }

        internal static void SetButtonSprites(
            Button button,
            UiAssetBundle assets,
            string normal,
            string highlighted,
            string pressed,
            string disabled)
        {
            if (button == null)
                return;
            Image image = button.targetGraphic as Image;
            if (image == null)
                image = button.GetComponent<Image>();
            if (image != null)
                image.sprite = assets.GetSprite(normal);
            SpriteState state = button.spriteState;
            state.highlightedSprite = assets.GetSprite(highlighted);
            state.pressedSprite = assets.GetSprite(pressed);
            state.disabledSprite = assets.GetSprite(disabled);
            button.spriteState = state;
            button.transition = Selectable.Transition.SpriteSwap;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

    }
}
