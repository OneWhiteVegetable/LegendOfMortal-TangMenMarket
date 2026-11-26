using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Shared
{
    // The native scene HUD, shop and upgrade panels all use the same Money / Icon + Text layout.
    internal sealed class NativeMoneyHud : MonoBehaviour
    {
        private Text _text;
        private RectTransform _frame;
        private float _baseTextWidth;
        private float _baseFrameWidth;
        private float _baseFrameX;
        private string _lastText;
        private Font _lastFont;
        private int _lastSize;
        private float _lastDensity;

        internal static void Attach(Text text)
        {
            RectTransform frame = text.transform.parent as RectTransform;
            if (frame == null || frame.name != "Money" || frame.Find("Icon") == null)
                return;
            Image paper = frame.GetComponent<Image>();
            if (paper == null || paper.sprite == null || paper.sprite.name != "back_money" ||
                text.GetComponent<NativeMoneyHud>() != null)
                return;

            NativeMoneyHud hud = text.gameObject.AddComponent<NativeMoneyHud>();
            hud._text = text;
            hud._frame = frame;
            hud._baseTextWidth = text.rectTransform.rect.width;
            hud._baseFrameWidth = frame.rect.width;
            hud._baseFrameX = frame.anchoredPosition.x;
            frame.gameObject.AddComponent<MoneyFrameSlicing>().SetOriginalWidth(hud._baseFrameWidth);
        }

        private void OnEnable()
        {
            _lastText = null;
        }

        private void LateUpdate()
        {
            if (_text == null || _text.font == null)
                return;
            float density = _text.pixelsPerUnit;
            if (_lastText == _text.text && _lastFont == _text.font && _lastSize == _text.fontSize &&
                Mathf.Approximately(_lastDensity, density))
                return;
            _lastText = _text.text;
            _lastSize = _text.fontSize;
            _lastFont = _text.font;
            _lastDensity = density;

            // Measure at the game's native font size, even when another mod enables best-fit.
            TextGenerationSettings settings = _text.GetGenerationSettings(Vector2.zero);
            settings.resizeTextForBestFit = false;
            float inkWidth = _text.cachedTextGeneratorForLayout.GetPreferredWidth(_lastText, settings) / density;
            float extra = Mathf.Max(0f, Mathf.Ceil(inkWidth + 2f) - _baseTextWidth);
            _text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _baseTextWidth + extra);
            _frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _baseFrameWidth + extra);
            Vector2 position = _frame.anchoredPosition;
            position.x = _baseFrameX - extra * (1f - _frame.pivot.x);
            _frame.anchoredPosition = position;
        }
    }
}
